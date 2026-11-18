using Cassandra.Application.Settings;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

/// <summary>Runs the pipeline on a background task; owns Start / Pause / Resume.</summary>
public sealed class CaptureLoopService : ICaptureLoop
{
    private readonly TranslationPipeline _pipeline;
    private readonly ISettingsService _settings;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;

    public CaptureLoopService(TranslationPipeline pipeline, ISettingsService settings)
    {
        _pipeline = pipeline;
        _settings = settings;
    }

    public PipelineState State { get; private set; } = PipelineState.Stopped;

    public event Action<PipelineState>? StateChanged;
    public event Action<IReadOnlyList<TranslatedLine>>? LinesTranslated;
    public event Action<Exception>? Failed;

    public void Start()
    {
        lock (_gate)
        {
            if (State == PipelineState.Running)
            {
                return;
            }

            _pipeline.ResetFrame();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _loop = Task.Run(() => RunAsync(ct), ct);
            SetState(PipelineState.Running);
        }
    }

    public async Task PauseAsync()
    {
        Task loop;
        lock (_gate)
        {
            if (State != PipelineState.Running)
            {
                return;
            }

            _cts!.Cancel();
            loop = _loop;
            SetState(PipelineState.Paused);
        }

        try
        {
            await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Resume() => Start();

    public void Dispose() => _cts?.Cancel();

    private async Task RunAsync(CancellationToken ct)
    {
        var lastRegion = _settings.Current.Capture;
        var lastPair = _settings.Current.Languages;

        while (!ct.IsCancellationRequested)
        {
            var settings = _settings.Current;
            if (settings.Languages != lastPair)
            {
                _pipeline.Reset();
            }
            else if (settings.Capture != lastRegion)
            {
                _pipeline.ResetFrame();
            }

            lastRegion = settings.Capture;
            lastPair = settings.Languages;

            try
            {
                if (!settings.Capture.IsEmpty)
                {
                    var outcome = await _pipeline.RunOnceAsync(settings.Capture, settings.Languages, ct).ConfigureAwait(false);
                    if (outcome is PipelineOutcome.Translated translated)
                    {
                        LinesTranslated?.Invoke(translated.Lines);
                    }
                }

                await Task.Delay(settings.CaptureIntervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex);
                try
                {
                    await Task.Delay(ex is ModelNotInstalledException ? 5000 : 2000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void SetState(PipelineState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
