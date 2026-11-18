namespace Cassandra.Application.Translation;

public interface IManualTranslationService
{
    /// <summary>Retranslates corrected original text in the chat direction (EN to RU).</summary>
    Task<string> TranslateOriginalAsync(string text, CancellationToken ct);

    /// <summary>Translates the player's own text in the reverse direction (RU to EN).</summary>
    Task<string> TranslateReplyAsync(string text, CancellationToken ct);
}
