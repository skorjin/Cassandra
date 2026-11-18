using System.Net.Sockets;
using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Models;

public sealed class ModelManager : IModelManager
{
    private const int MaxAttempts = 8;

    private readonly IModelCatalog _catalog;
    private readonly IModelStore _store;
    private readonly IModelDownloader _downloader;
    private readonly ISettingsService _settings;
    private readonly Func<int, TimeSpan> _backoff;
    private readonly object _gate = new();

    private IReadOnlyList<ModelVariant> _variants = Array.Empty<ModelVariant>();
    private CancellationTokenSource? _cts;
    private Task _worker = Task.CompletedTask;
    private DateTime _lastProgressEvent = DateTime.MinValue;

    public ModelManager(
        IModelCatalog catalog,
        IModelStore store,
        IModelDownloader downloader,
        ISettingsService settings,
        Func<int, TimeSpan>? backoff = null)
    {
        _catalog = catalog;
        _store = store;
        _downloader = downloader;
        _settings = settings;
        _backoff = backoff ?? DefaultBackoff;
    }

    public IReadOnlyList<ModelStatus> Statuses { get; private set; } = Array.Empty<ModelStatus>();

    public DownloadState State { get; private set; } = new();

    public string? ActiveModelId => _settings.Current.ActiveModelId;

    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _variants = await _catalog.LoadAsync(ct).ConfigureAwait(false);
        RefreshStatuses();

        var active = ActiveModelId;
        if (active is not null && !Statuses.Any(s => s.Variant.Id == active && s.State is ModelState.Installed or ModelState.UpdateAvailable))
        {
            _settings.Update(s => s with { ActiveModelId = null }); // the files are gone
        }

        Changed?.Invoke();
    }

    public void StartDownload(string variantId)
    {
        lock (_gate)
        {
            if (State.IsBusy)
            {
                return;
            }

            var variant = _variants.FirstOrDefault(v => v.Id == variantId)
                          ?? throw new ArgumentException($"Unknown model '{variantId}'.", nameof(variantId));

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var already = Statuses.FirstOrDefault(s => s.Variant.Id == variantId)?.DownloadedBytes ?? 0;
            SetState(new DownloadState(DownloadPhase.Downloading, variantId, already, variant.TotalBytes));
            _worker = Task.Run(() => RunAsync(variant, ct));
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _cts?.Cancel();
        }
    }

    public void Activate(string variantId)
    {
        var status = Statuses.FirstOrDefault(s => s.Variant.Id == variantId);
        if (status is null || status.State is ModelState.NotInstalled or ModelState.Partial)
        {
            throw new InvalidOperationException("Модель ещё не скачана.");
        }

        _settings.Update(s => s with { ActiveModelId = variantId });
        Changed?.Invoke();
    }

    public async Task DeleteAsync(string variantId)
    {
        if (State.IsBusy && State.VariantId == variantId)
        {
            Cancel();
            await _worker.ConfigureAwait(false);
        }

        await _store.DeleteAsync(variantId).ConfigureAwait(false);
        if (ActiveModelId == variantId)
        {
            _settings.Update(s => s with { ActiveModelId = null });
        }

        RefreshStatuses();
        SetState(new DownloadState());
    }

    /// <summary>Downloads with automatic retries. Ends in Idle (done), Paused (player stopped) or Failed (gave up).</summary>
    private async Task RunAsync(ModelVariant variant, CancellationToken ct)
    {
        var attempt = 0;
        var bytesAtLastFailure = -1L;

        while (true)
        {
            try
            {
                var progress = new SynchronousProgress(p => OnProgress(variant, p, attempt));
                await _downloader.DownloadAsync(variant, progress, ct).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Pause();
                return;
            }
            catch (InsufficientDiskSpaceException ex)
            {
                RefreshStatuses();
                SetState(State with { Phase = DownloadPhase.Failed, Message = ex.Message });
                return;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // progress since the last failure means the connection works, so start counting again
                attempt = State.DownloadedBytes > bytesAtLastFailure ? 1 : attempt + 1;
                bytesAtLastFailure = State.DownloadedBytes;

                if (attempt > MaxAttempts)
                {
                    RefreshStatuses();
                    SetState(State with
                    {
                        Phase = DownloadPhase.Failed,
                        RetryIn = null,
                        Message = "Связь прервана. Скачанное сохранено, нажмите «Продолжить», когда сеть появится.",
                    });
                    return;
                }

                if (!await WaitBeforeRetryAsync(attempt, ex, ct).ConfigureAwait(false))
                {
                    Pause();
                    return;
                }
            }
            catch (Exception ex)
            {
                RefreshStatuses();
                SetState(State with { Phase = DownloadPhase.Failed, Message = ex.Message });
                return;
            }
        }

        RefreshStatuses();
        if (ActiveModelId is null || ActiveModelId == variant.Id)
        {
            _settings.Update(s => s with { ActiveModelId = variant.Id });
        }

        SetState(new DownloadState());
    }

    private void Pause()
    {
        RefreshStatuses();
        SetState(State with { Phase = DownloadPhase.Paused, Message = "Загрузка приостановлена. Скачанное сохранено.", RetryIn = null });
    }

    /// <returns>False when the player cancelled while waiting.</returns>
    private async Task<bool> WaitBeforeRetryAsync(int attempt, Exception error, CancellationToken ct)
    {
        var until = DateTime.UtcNow + _backoff(attempt);
        try
        {
            while (true)
            {
                var left = until - DateTime.UtcNow;
                if (left <= TimeSpan.Zero)
                {
                    return true;
                }

                SetState(State with
                {
                    Phase = DownloadPhase.WaitingToRetry,
                    Attempt = attempt,
                    RetryIn = left,
                    Message = $"Нет связи ({Describe(error)}). Повтор через {Math.Ceiling(left.TotalSeconds)} с…",
                });
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500, left.TotalMilliseconds)), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void OnProgress(ModelVariant variant, DownloadProgress p, int attempt)
    {
        State = new DownloadState(DownloadPhase.Downloading, variant.Id, p.DownloadedBytes, p.TotalBytes, attempt);

        var now = DateTime.UtcNow;
        if (now - _lastProgressEvent > TimeSpan.FromMilliseconds(250))
        {
            _lastProgressEvent = now;
            Changed?.Invoke();
        }
    }

    private void SetState(DownloadState state)
    {
        State = state;
        Changed?.Invoke();
    }

    private void RefreshStatuses() => Statuses = _store.GetStatuses(_variants);

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException or SocketException or TimeoutException or OperationCanceledException;

    private static string Describe(Exception ex) => ex switch
    {
        TimeoutException or OperationCanceledException => "таймаут",
        HttpRequestException { StatusCode: { } code } => $"HTTP {(int)code}",
        _ => "ошибка сети",
    };

    private static TimeSpan DefaultBackoff(int attempt) => attempt switch
    {
        1 => TimeSpan.FromSeconds(2),
        2 => TimeSpan.FromSeconds(5),
        3 => TimeSpan.FromSeconds(10),
        4 => TimeSpan.FromSeconds(20),
        _ => TimeSpan.FromSeconds(30),
    };
}

/// <summary>Unlike <see cref="Progress{T}"/> this runs on the reporting thread, so state never lags behind a failure.</summary>
internal sealed class SynchronousProgress(Action<DownloadProgress> handler) : IProgress<DownloadProgress>
{
    public void Report(DownloadProgress value) => handler(value);
}
