using Cassandra.Application.Settings;
using Cassandra.Application.Translation;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class ManualTranslationTests
{
    private sealed class RecordingTranslator : ITextTranslator
    {
        public LanguagePair? LastPair;
        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct)
        {
            LastPair = pair;
            return Task.FromResult($"[{pair}] {text}");
        }
    }

    private sealed class MemoryStore : ISettingsStore
    {
        public AppSettings Saved = new();
        public AppSettings Load() => new();
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Reply_goes_in_the_reverse_direction()
    {
        var translator = new RecordingTranslator();
        var service = new ManualTranslationService(translator, new SettingsService(new MemoryStore()));

        var result = await service.TranslateReplyAsync("  привет  ", default);

        Assert.Equal(new LanguagePair("ru", "en"), translator.LastPair);
        Assert.Equal("[ru→en] привет", result);
    }

    [Fact]
    public async Task Original_goes_in_the_chat_direction()
    {
        var translator = new RecordingTranslator();
        var service = new ManualTranslationService(translator, new SettingsService(new MemoryStore()));

        await service.TranslateOriginalAsync("hello", default);

        Assert.Equal(LanguagePair.EnRu, translator.LastPair);
    }

    [Fact]
    public async Task Blank_text_is_not_sent_to_translator()
    {
        var translator = new RecordingTranslator();
        var service = new ManualTranslationService(translator, new SettingsService(new MemoryStore()));

        Assert.Equal(string.Empty, await service.TranslateReplyAsync("   ", default));
        Assert.Null(translator.LastPair);
    }

    [Fact]
    public async Task Settings_changes_are_saved_once_after_debounce()
    {
        var store = new MemoryStore();
        var settings = new SettingsService(store, TimeSpan.FromMilliseconds(20));

        settings.Update(s => s with { Opacity = 0.5 });
        settings.Update(s => s with { Opacity = 0.6 });
        await Task.Delay(200);

        Assert.Equal(0.6, store.Saved.Opacity);
    }
}
