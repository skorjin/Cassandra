using System.Text.RegularExpressions;

namespace Cassandra.Application.Pipeline;

public readonly record struct ParsedChatLine(string? Speaker, string Message);

/// <summary>Splits "[Team] Nick: text" so that only the text is translated and nicknames stay untouched.</summary>
public static partial class ChatLineParser
{
    [GeneratedRegex(@"^\s*(?<speaker>(?:[\[\(<][^\]\)>]{1,20}[\]\)>]\s*)*[\p{L}\p{N}_\.\-]{2,24})\s*[:>]\s*(?<msg>\S.*)$")]
    private static partial Regex LinePattern();

    public static ParsedChatLine Parse(string line)
    {
        var m = LinePattern().Match(line);
        return m.Success
            ? new ParsedChatLine(m.Groups["speaker"].Value.Trim(), m.Groups["msg"].Value.Trim())
            : new ParsedChatLine(null, line.Trim());
    }
}
