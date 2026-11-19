using Cassandra.Application.Translation;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class TargetLanguageAwareTranslatorTests
{
    private sealed class MarkingTranslator : ITextTranslator
    {
        public int Calls;

        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult("<" + text + ">");
        }
    }

    [Theory]
    [InlineData("[3. Оборона: Тернистая долина] : Мыс Тернистой", "en", "ru")]
    [InlineData("<) [Бастион Дженкинс] говорит: help", "en", "ru")]
    [InlineData("& [Sameh] (& Sameh) входит в сеть.", "en", "ru")]
    [InlineData("Do you have the quest?", "ru", "en")]
    public async Task Text_in_the_target_language_never_reaches_the_model(string text, string from, string to)
    {
        var inner = new MarkingTranslator();
        var translator = new TargetLanguageAwareTranslator(inner);

        var result = await translator.TranslateAsync(text, new LanguagePair(from, to), default);

        Assert.Equal(text.Trim(), result);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Text_in_the_source_language_is_translated()
    {
        var inner = new MarkingTranslator();
        var translator = new TargetLanguageAwareTranslator(inner);

        var result = await translator.TranslateAsync("Anyone have the quest?", LanguagePair.EnRu, default);

        Assert.Equal("<Anyone have the quest?>", result);
    }
}
