using Cassandra.Domain.Models;

namespace Cassandra.Application.Models;

public enum DownloadPhase
{
    Idle,
    Downloading,

    /// <summary>The connection failed; the next attempt starts automatically after <see cref="DownloadState.RetryIn"/>.</summary>
    WaitingToRetry,

    /// <summary>Stopped by the player. Received data is kept.</summary>
    Paused,

    /// <summary>Gave up (no more attempts, disk full, ...). Received data is kept, the player can continue.</summary>
    Failed,
}

public sealed record DownloadState(
    DownloadPhase Phase = DownloadPhase.Idle,
    string? VariantId = null,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    int Attempt = 0,
    string? Message = null,
    TimeSpan? RetryIn = null)
{
    public double Fraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)DownloadedBytes / TotalBytes, 0, 1);

    public bool IsBusy => Phase is DownloadPhase.Downloading or DownloadPhase.WaitingToRetry;
}

/// <summary>Catalog of translation models, their install state, downloading with resume, and the active choice.</summary>
public interface IModelManager
{
    IReadOnlyList<ModelStatus> Statuses { get; }
    DownloadState State { get; }
    string? ActiveModelId { get; }

    /// <summary>Raised from background threads whenever statuses, progress or the active model change.</summary>
    event Action? Changed;

    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Starts or resumes downloading a variant. Network drops are retried automatically.</summary>
    void StartDownload(string variantId);

    /// <summary>Pauses the running download and keeps what was received.</summary>
    void Cancel();

    void Activate(string variantId);

    Task DeleteAsync(string variantId);
}
