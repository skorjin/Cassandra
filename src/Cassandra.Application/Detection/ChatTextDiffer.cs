using Cassandra.Domain.Abstractions;

namespace Cassandra.Application.Detection;

/// <summary>
/// Chat scrolls up: the tail of the previous OCR result becomes the head of the new one.
/// New lines are whatever follows the longest such overlap. Lines are compared fuzzily because OCR is noisy.
/// </summary>
public sealed class ChatTextDiffer : IChatTextDiffer
{
    private readonly double _minSimilarity;
    private IReadOnlyList<string> _previous = Array.Empty<string>();

    public ChatTextDiffer(double minSimilarity = 0.85) => _minSimilarity = minSimilarity;

    public IReadOnlyList<string> GetNewLines(IReadOnlyList<string> currentLines)
    {
        var current = currentLines.Where(l => Normalize(l).Length > 0).ToList();
        var previous = _previous;
        _previous = current;

        var max = Math.Min(previous.Count, current.Count);
        for (var k = max; k >= 1; k--)
        {
            if (OverlapMatches(previous, current, k))
            {
                return current.Skip(k).ToList();
            }
        }

        return current;
    }

    public void Reset() => _previous = Array.Empty<string>();

    private bool OverlapMatches(IReadOnlyList<string> previous, IReadOnlyList<string> current, int k)
    {
        for (var i = 0; i < k; i++)
        {
            if (!Similar(previous[previous.Count - k + i], current[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool Similar(string a, string b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na == nb)
        {
            return true;
        }

        var longest = Math.Max(na.Length, nb.Length);
        return longest > 0 && 1.0 - (double)Levenshtein(na, nb) / longest >= _minSimilarity;
    }

    private static string Normalize(string s) =>
        new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }

            (prev, cur) = (cur, prev);
        }

        return prev[b.Length];
    }
}
