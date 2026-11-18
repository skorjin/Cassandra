using System.Text.Json;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Settings;

/// <summary>Stores window positions, opacity, tint and languages as a small JSON file in %AppData%.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = false };

    private readonly string _path;

    public JsonSettingsStore(string? path = null) => _path = path ?? AppPaths.SettingsFile;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // corrupt or unreadable file: fall back to defaults
        }

        return new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings, Options), ct).ConfigureAwait(false);
    }
}
