namespace Cassandra.Domain.Models;

/// <param name="Path">Relative path inside the model directory, e.g. "en-ru/encoder_model.onnx".</param>
public sealed record ModelFile(string Path, string Url, long Size, string Sha256);

/// <summary>A downloadable set of translation models (both directions) in one precision.</summary>
public sealed record ModelVariant(
    string Id,
    string Name,
    string Precision,
    string Description,
    int Version,
    IReadOnlyList<ModelFile> Files)
{
    public long TotalBytes => Files.Sum(f => f.Size);
}

public sealed record ModelCatalogData(int Schema, IReadOnlyList<ModelVariant> Variants);

public enum ModelState
{
    NotInstalled,

    /// <summary>A download was started and interrupted; already received bytes are kept for resuming.</summary>
    Partial,

    Installed,

    /// <summary>Installed and usable, but the catalog lists a newer version.</summary>
    UpdateAvailable,
}

public sealed record ModelStatus(ModelVariant Variant, ModelState State, long DownloadedBytes);

public sealed record DownloadProgress(long DownloadedBytes, long TotalBytes, string? CurrentFile);
