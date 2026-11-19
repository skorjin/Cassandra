using Cassandra.Application.Pipeline;

namespace Cassandra.Application.Tests;

public class ScriptDetectorTests
{
    [Theory]
    [InlineData("Сбор данных отключён. Используйте \"/qlt consent\"", "ru", true)]
    [InlineData("Ведите себя разумно", "ru", true)]
    [InlineData("loaded 7478 items (data 2026-11-17)", "ru", false)]
    [InlineData("hello there", "en", true)]
    [InlineData("привет", "en", false)]
    [InlineData("", "ru", false)]
    public void Detects_whether_text_is_already_in_the_target_language(string text, string language, bool expected) =>
        Assert.Equal(expected, ScriptDetector.IsAlreadyIn(text, language));
}
