using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Translation;

public sealed class ManualTranslationService : IManualTranslationService
{
    private readonly ITextTranslator _translator;
    private readonly ISettingsService _settings;

    public ManualTranslationService(ITextTranslator translator, ISettingsService settings)
    {
        _translator = translator;
        _settings = settings;
    }

    public Task<string> TranslateOriginalAsync(string text, CancellationToken ct) =>
        TranslateAsync(text, _settings.Current.Languages, ct);

    public Task<string> TranslateReplyAsync(string text, CancellationToken ct) =>
        TranslateAsync(text, _settings.Current.Languages.Reversed(), ct);

    private async Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : await _translator.TranslateAsync(text.Trim(), pair, ct).ConfigureAwait(false);
}
