using Cassandra.Application.Pipeline;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Translation;

/// <summary>
/// Wraps the real translator: text that is already in the target language is returned untouched. Every path
/// (chat lines, the original editor, replies) goes through here, so a translation model never mangles it.
/// </summary>
public sealed class TargetLanguageAwareTranslator(ITextTranslator inner) : ITextTranslator, IDisposable
{
    public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
        ScriptDetector.IsAlreadyIn(text, pair.Target) ? Task.FromResult(text.Trim()) : inner.TranslateAsync(text, pair, ct);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
