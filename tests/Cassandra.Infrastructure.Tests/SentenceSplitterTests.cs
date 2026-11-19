using Cassandra.Infrastructure.Translation;

namespace Cassandra.Infrastructure.Tests;

public class SentenceSplitterTests
{
    [Fact]
    public void Splits_on_sentence_ends()
    {
        var parts = SentenceSplitter.Split("привет! есть у кого-то квесты тут? поделитесь пожалуйста!");
        Assert.Equal(["привет!", "есть у кого-то квесты тут?", "поделитесь пожалуйста!"], parts);
    }

    [Fact]
    public void Keeps_single_sentence_and_splits_lines()
    {
        Assert.Equal(["one two"], SentenceSplitter.Split("one two"));
        Assert.Equal(["a", "b"], SentenceSplitter.Split("a\nb"));
        Assert.Equal(["v1.2 is out"], SentenceSplitter.Split("v1.2 is out"));
    }
}
