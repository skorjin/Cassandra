using Cassandra.Domain.Abstractions;
using Velopack;
using Velopack.Sources;

namespace Cassandra.Infrastructure.Updates;

/// <summary>Application self-update through Velopack, with releases published on GitHub.</summary>
public sealed class VelopackAppUpdater : IAppUpdater
{
    public const string DefaultRepository = "https://github.com/skorjin/Cassandra";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public VelopackAppUpdater(string repositoryUrl = DefaultRepository)
        : this(new GithubSource(repositoryUrl, accessToken: null, prerelease: false))
    {
    }

    public VelopackAppUpdater(IUpdateSource source) => _manager = new UpdateManager(source);

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString()
        ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public async Task<AppUpdateInfo?> CheckAsync(CancellationToken ct)
    {
        if (!_manager.IsInstalled)
        {
            return null;
        }

        _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _pending is null
            ? null
            : new AppUpdateInfo(_pending.TargetFullRelease.Version.ToString(), _pending.TargetFullRelease.Size);
    }

    public async Task DownloadAndRestartAsync(IProgress<int>? percent, CancellationToken ct)
    {
        var update = _pending ?? throw new InvalidOperationException("Сначала проверьте наличие обновления.");
        await _manager.DownloadUpdatesAsync(update, p => percent?.Report(p), ct).ConfigureAwait(false);
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}
