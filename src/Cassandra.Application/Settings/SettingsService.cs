using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Settings;

/// <summary>Holds the live settings and persists them with a debounce so slider drags do not hit the disk.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;
    private readonly TimeSpan _saveDelay;
    private readonly object _gate = new();
    private CancellationTokenSource? _pendingSave;

    public SettingsService(ISettingsStore store, TimeSpan? saveDelay = null)
    {
        _store = store;
        _saveDelay = saveDelay ?? TimeSpan.FromMilliseconds(500);
        Current = store.Load();
    }

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated;
        lock (_gate)
        {
            updated = change(Current);
            if (updated == Current)
            {
                return;
            }

            Current = updated;
            _pendingSave?.Cancel();
            _pendingSave = new CancellationTokenSource();
            _ = SaveLaterAsync(updated, _pendingSave.Token);
        }

        Changed?.Invoke(updated);
    }

    private async Task SaveLaterAsync(AppSettings settings, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_saveDelay, ct).ConfigureAwait(false);
            await _store.SaveAsync(settings, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer update
        }
        catch (IOException)
        {
            // settings are a convenience; failing to persist must not crash the overlay
        }
    }
}
