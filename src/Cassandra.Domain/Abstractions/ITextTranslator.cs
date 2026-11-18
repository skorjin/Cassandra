using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface ITextTranslator
{
    Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct);
}
