using System.Text.RegularExpressions;

namespace Cassandra.Infrastructure.Translation;

internal static partial class SentenceSplitter
{
    // a sentence ends with . ! ? or an ellipsis followed by whitespace; line breaks always separate
    [GeneratedRegex(@"(?<=[.!?…]+[""')\]]*)\s+|[\r\n]+")]
    private static partial Regex Boundary();

    public static IReadOnlyList<string> Split(string text) =>
        Boundary().Split(text).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}
