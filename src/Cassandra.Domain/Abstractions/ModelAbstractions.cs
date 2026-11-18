using Cassandra.Domain.Models;

namespace Cassandra.Domain.Abstractions;

public interface IModelCatalog
{
    /// <summary>Remote catalog when reachable, otherwise the one shipped with the app.</summary>
    Task<IReadOnlyList<ModelVariant>> LoadAsync(CancellationToken ct);
}

public interface IModelStore
{
    IReadOnlyList<ModelStatus> GetStatuses(IReadOnlyList<ModelVariant> variants);

    /// <summary>Directory of an installed variant, or null when it is not installed.</summary>
    string? GetInstalledDirectory(string variantId);

    Task DeleteAsync(string variantId);
}

public interface IModelDownloader
{
    /// <summary>
    /// One download pass that resumes from whatever is already on disk. Throws on network or disk errors;
    /// the caller decides whether to retry. Completes only when every file is verified and the variant is installed.
    /// </summary>
    Task DownloadAsync(ModelVariant variant, IProgress<DownloadProgress>? progress, CancellationToken ct);
}

public interface IActiveModelProvider
{
    string? ActiveModelId { get; }
}

public sealed record AppUpdateInfo(string Version, long SizeBytes);

public interface IAppUpdater
{
    /// <summary>False when running from a build folder: updates only work for an installed app.</summary>
    bool IsInstalled { get; }

    string CurrentVersion { get; }

    Task<AppUpdateInfo?> CheckAsync(CancellationToken ct);

    /// <summary>Downloads the update found by <see cref="CheckAsync"/> and restarts into the new version.</summary>
    Task DownloadAndRestartAsync(IProgress<int>? percent, CancellationToken ct);
}
