using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Translation;

/// <summary>
/// Local Marian translation through ONNX Runtime. Models load lazily per direction and are released when the
/// active model directory changes (the player picked another variant or an update was installed). No network.
/// </summary>
public sealed class OnnxMarianTranslator : ITextTranslator, IDisposable
{
    private readonly Func<string?> _activeDirectory;
    private readonly Dictionary<string, MarianModel> _models = new();
    private readonly object _gate = new();
    private string? _loadedFrom;

    /// <param name="activeDirectory">Returns the directory of the model variant in use, or null when none is installed.</param>
    public OnnxMarianTranslator(Func<string?> activeDirectory) => _activeDirectory = activeDirectory;

    public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(text)
            ? Task.FromResult(string.Empty)
            : Task.Run(() => GetModel(pair).Translate(text, ct), ct);

    private MarianModel GetModel(LanguagePair pair)
    {
        var root = _activeDirectory() ?? throw new ModelNotInstalledException();
        var key = $"{pair.Source}-{pair.Target}";

        lock (_gate)
        {
            if (_loadedFrom != root)
            {
                DisposeModels();
                _loadedFrom = root;
            }

            if (_models.TryGetValue(key, out var model))
            {
                return model;
            }

            var directory = Path.Combine(root, key);
            if (!File.Exists(Path.Combine(directory, "encoder_model.onnx")))
            {
                throw new ModelNotInstalledException();
            }

            model = new MarianModel(directory);
            _models[key] = model;
            return model;
        }
    }

    private void DisposeModels()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }

        _models.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeModels();
        }
    }
}
