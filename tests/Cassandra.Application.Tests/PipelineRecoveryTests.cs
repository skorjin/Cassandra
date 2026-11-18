using Cassandra.Application.Detection;
using Cassandra.Application.Pipeline;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class PipelineRecoveryTests
{
    private sealed class StableCapturer : IScreenCapturer
    {
        public Frame Capture(CaptureRegion region) => new(8, 8, new byte[8 * 8 * 4]);
    }

    private sealed class FixedOcr : IOcrEngine
    {
        public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct) =>
            Task.FromResult(new RecognizedText(["Nick: enemy on the roof"]));
    }

    private sealed class SwitchableTranslator : ITextTranslator
    {
        public bool Installed;

        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
            Installed ? Task.FromResult("перевод") : throw new ModelNotInstalledException();
    }

    [Fact]
    public async Task Lines_seen_while_no_model_was_installed_are_translated_once_it_is()
    {
        var translator = new SwitchableTranslator();
        var pipeline = new TranslationPipeline(new StableCapturer(), new FrameChangeDetector(), new FixedOcr(), new ChatTextDiffer(), translator);
        var region = new CaptureRegion(0, 0, 8, 8);

        await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);                       // first frame counts as a change
        await Assert.ThrowsAsync<ModelNotInstalledException>(() => pipeline.RunOnceAsync(region, LanguagePair.EnRu, default));

        translator.Installed = true;
        var outcome = await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);

        var translated = Assert.IsType<PipelineOutcome.Translated>(outcome);
        Assert.Equal("перевод", translated.Lines.Single().Translated);
    }
}
