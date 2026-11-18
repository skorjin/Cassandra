using System.Text.RegularExpressions;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Models;

/// <summary>
/// Layout under %LocalAppData%\CassandraData\models (outside the installer's folder, so models survive reinstalls):
/// <code>
/// {variant}\v{version}\...          verified, ready to use (contains the ".ready" marker)
/// {variant}\staging-v{version}\...  download in progress; "*.part" files are resumable
/// </code>
/// A version directory never changes after it is committed, so an update installs next to the old one and the
/// translator simply starts using the new path.
/// </summary>
public sealed partial class ModelStorage : IModelStore
{
    internal const string ReadyMarker = ".ready";

    public ModelStorage(string? root = null) =>
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CassandraData", "models");

    public string Root { get; }

    public IReadOnlyList<ModelStatus> GetStatuses(IReadOnlyList<ModelVariant> variants) =>
        variants.Select(GetStatus).ToList();

    public string? GetInstalledDirectory(string variantId)
    {
        var version = LatestInstalledVersion(variantId);
        return version is null ? null : InstalledDirectory(variantId, version.Value);
    }

    public async Task DeleteAsync(string variantId)
    {
        var dir = Path.Combine(Root, variantId);
        for (var attempt = 0; attempt < 5 && Directory.Exists(dir); attempt++)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                await Task.Delay(200).ConfigureAwait(false); // files may still be released by the translator
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
        }
    }

    internal string InstalledDirectory(string variantId, int version) => Path.Combine(Root, variantId, $"v{version}");

    internal string StagingDirectory(ModelVariant variant) => Path.Combine(Root, variant.Id, $"staging-v{variant.Version}");

    /// <summary>Marks the staging directory as complete and turns it into the installed version. Older versions are removed.</summary>
    internal void Commit(ModelVariant variant)
    {
        var staging = StagingDirectory(variant);
        File.WriteAllText(Path.Combine(staging, ReadyMarker), variant.Version.ToString());

        var target = InstalledDirectory(variant.Id, variant.Version);
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(staging, target);

        foreach (var old in Directory.EnumerateDirectories(Path.Combine(Root, variant.Id)))
        {
            if (old != target && InstalledVersionRegex().IsMatch(Path.GetFileName(old)))
            {
                TryDelete(old); // may still be in use until the translator switches; cleaned up next time then
            }
        }
    }

    internal static long DirectoryBytes(string dir) =>
        Directory.Exists(dir)
            ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Where(f => f.Name != ReadyMarker).Sum(f => f.Length)
            : 0;

    private ModelStatus GetStatus(ModelVariant variant)
    {
        var installed = LatestInstalledVersion(variant.Id);
        var staged = DirectoryBytes(StagingDirectory(variant));

        if (installed is { } version)
        {
            return version >= variant.Version
                ? new ModelStatus(variant, ModelState.Installed, variant.TotalBytes)
                : new ModelStatus(variant, ModelState.UpdateAvailable, staged);
        }

        return staged > 0
            ? new ModelStatus(variant, ModelState.Partial, staged)
            : new ModelStatus(variant, ModelState.NotInstalled, 0);
    }

    private int? LatestInstalledVersion(string variantId)
    {
        var dir = Path.Combine(Root, variantId);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        int? best = null;
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var match = InstalledVersionRegex().Match(Path.GetFileName(sub));
            if (match.Success && File.Exists(Path.Combine(sub, ReadyMarker)))
            {
                var version = int.Parse(match.Groups[1].Value);
                if (best is null || version > best)
                {
                    best = version;
                }
            }
        }

        return best;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [GeneratedRegex(@"^v(\d+)$")]
    private static partial Regex InstalledVersionRegex();
}
