using System.Reflection;
using System.Text.Json;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Models;

/// <summary>
/// Reads the model catalog. The copy embedded in the app always works offline; a newer catalog published
/// next to the releases is picked up when the network allows, so models can be updated without a new app version.
/// </summary>
public sealed class ModelCatalogLoader : IModelCatalog
{
    public const string DefaultRemoteUrl = "https://raw.githubusercontent.com/skorjin/Cassandra/main/catalog/models.json";
    private const int SupportedSchema = 1;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string? _remoteUrl;
    private readonly HttpClient _client;

    public ModelCatalogLoader(string? remoteUrl = DefaultRemoteUrl, HttpClient? client = null)
    {
        _remoteUrl = remoteUrl;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    public async Task<IReadOnlyList<ModelVariant>> LoadAsync(CancellationToken ct)
    {
        var embedded = Parse(ReadEmbedded()) ?? throw new InvalidDataException("Встроенный каталог моделей повреждён.");

        if (!string.IsNullOrEmpty(_remoteUrl))
        {
            try
            {
                var text = await _client.GetStringAsync(_remoteUrl, ct).ConfigureAwait(false);
                if (Parse(text) is { } remote)
                {
                    return remote;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // offline or unreachable: the embedded catalog is enough
            }
        }

        return embedded;
    }

    internal static IReadOnlyList<ModelVariant>? Parse(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<ModelCatalogData>(json, Json);
            return data is { Schema: SupportedSchema, Variants.Count: > 0 } && data.Variants.All(IsValid) ? data.Variants : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValid(ModelVariant v) =>
        !string.IsNullOrWhiteSpace(v.Id) && v.Version > 0 && v.Files is { Count: > 0 } &&
        v.Files.All(f => !string.IsNullOrWhiteSpace(f.Path) && f.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                         f.Size > 0 && f.Sha256.Length == 64);

    private static string ReadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.models.json")
                           ?? throw new InvalidDataException("Каталог моделей не встроен в сборку.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
