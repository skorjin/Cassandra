using Cassandra.Application.Detection;

namespace Cassandra.Application.Tests;

public class ChatTextDifferTests
{
    [Fact]
    public void First_call_returns_all_lines()
    {
        var differ = new ChatTextDiffer();
        Assert.Equal(new[] { "a one", "b two" }, differ.GetNewLines(new[] { "a one", "b two" }));
    }

    [Fact]
    public void Appended_line_is_the_only_new_one()
    {
        var differ = new ChatTextDiffer();
        differ.GetNewLines(new[] { "Kowalski: push left", "Rina: need medic" });
        var result = differ.GetNewLines(new[] { "Kowalski: push left", "Rina: need medic", "DarkFox: gg" });
        Assert.Equal(new[] { "DarkFox: gg" }, result);
    }

    [Fact]
    public void Scrolled_chat_returns_only_the_tail()
    {
        var differ = new ChatTextDiffer();
        differ.GetNewLines(new[] { "line A", "line B", "line C" });
        var result = differ.GetNewLines(new[] { "line B", "line C", "line D" });
        Assert.Equal(new[] { "line D" }, result);
    }

    [Fact]
    public void Faded_old_line_gives_no_new_lines()
    {
        var differ = new ChatTextDiffer();
        differ.GetNewLines(new[] { "line A", "line B", "line C" });
        Assert.Empty(differ.GetNewLines(new[] { "line B", "line C" }));
    }

    [Fact]
    public void Ocr_noise_in_old_lines_is_tolerated()
    {
        var differ = new ChatTextDiffer();
        differ.GetNewLines(new[] { "Kowalski: Enemy sniper on the roof" });
        var result = differ.GetNewLines(new[] { "Kowaiski: Enemy sniper on the r00f", "Rina: help" });
        Assert.Equal(new[] { "Rina: help" }, result);
    }

    [Fact]
    public void Unrelated_text_is_entirely_new()
    {
        var differ = new ChatTextDiffer();
        differ.GetNewLines(new[] { "aaaa bbbb" });
        Assert.Equal(new[] { "cccc dddd" }, differ.GetNewLines(new[] { "cccc dddd" }));
    }
}
