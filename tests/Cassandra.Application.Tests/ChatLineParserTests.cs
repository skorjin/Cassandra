using Cassandra.Application.Pipeline;

namespace Cassandra.Application.Tests;

public class ChatLineParserTests
{
    [Theory]
    [InlineData("Kowalski: Enemy sniper", "Kowalski", "Enemy sniper")]
    [InlineData("[Team] Kowalski: Enemy sniper", "[Team] Kowalski", "Enemy sniper")]
    [InlineData("[12:41] [All] DarkFox: gg", "[12:41] [All] DarkFox", "gg")]
    [InlineData("Just a plain sentence", null, "Just a plain sentence")]
    public void Splits_speaker_from_message(string line, string? speaker, string message)
    {
        var parsed = ChatLineParser.Parse(line);
        Assert.Equal(speaker, parsed.Speaker);
        Assert.Equal(message, parsed.Message);
    }
}
