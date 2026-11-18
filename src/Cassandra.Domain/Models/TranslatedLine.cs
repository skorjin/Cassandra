namespace Cassandra.Domain.Models;

/// <param name="Original">Full recognized line.</param>
/// <param name="Translated">Translated message text, without the speaker.</param>
/// <param name="Speaker">Tag and nickname such as "[Team] Kowalski", when the line had one.</param>
public sealed record TranslatedLine(string Original, string Translated, string? Speaker = null);
