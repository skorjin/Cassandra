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
            : Task.Run(() => TranslateSentences(text, pair, ct), ct);

    /// <summary>Marian is trained on single sentences: given several at once it tends to merge or drop some.</summary>
    private string TranslateSentences(string text, LanguagePair pair, CancellationToken ct)
    {
        var model = GetModel(pair);
        var parts = new List<string>();
        foreach (var sentence in SentenceSplitter.Split(text))
        {
            ct.ThrowIfCancellationRequested();
            var translated = model.Translate(sentence, ct);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                parts.Add(translated.Trim());
            }
        }

        return string.Join(' ', parts);
    }

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
