using Cassandra.Application.Pipeline;
using Cassandra.Application.Translation;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class TranslationPipelineTests
{
    private sealed class FakeCapturer : IScreenCapturer
    {
        public Frame Capture(CaptureRegion region) => new(1, 1, new byte[4]);
    }

    private sealed class ScriptedDetector(params bool[] answers) : IFrameChangeDetector
    {
        private int _i;
        public bool HasChanged(Frame frame) => answers[Math.Min(_i++, answers.Length - 1)];
        public void Reset() { }
    }

    private sealed class FakeOcr : IOcrEngine
    {
        public int Calls;
        public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new RecognizedText(new[] { "hello" }));
        }
    }

    private sealed class UpperTranslator : ITextTranslator
    {
        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
            Task.FromResult(text.ToUpperInvariant());
    }

    private static (TranslationPipeline Pipeline, FakeOcr Ocr) Build(params bool[] detectorAnswers)
    {
        var ocr = new FakeOcr();
        return (new TranslationPipeline(new FakeCapturer(), new ScriptedDetector(detectorAnswers), ocr, new UpperTranslator()), ocr);
    }

    [Fact]
    public async Task Changing_frame_postpones_ocr()
    {
        var (pipeline, ocr) = Build(true);
        var outcome = await pipeline.RunOnceAsync(new(0, 0, 1, 1), LanguagePair.EnRu, default);
        Assert.IsType<PipelineOutcome.Settling>(outcome);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task Stable_frame_after_change_runs_ocr_and_translation_once()
    {
        var (pipeline, ocr) = Build(true, false, false);
        var region = new CaptureRegion(0, 0, 1, 1);

        await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);
        var second = await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);
        var third = await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);

        var translated = Assert.IsType<PipelineOutcome.Translated>(second);
        Assert.Equal("HELLO", translated.Lines.Single().Translated);
        Assert.IsType<PipelineOutcome.Idle>(third);
        Assert.Equal(1, ocr.Calls);
    }

    private sealed class RussianOcr : IOcrEngine
    {
        public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct) =>
            Task.FromResult(new RecognizedText(new[] { "Nick: Привет, как дела" }));
    }

    [Fact]
    public async Task Text_already_in_the_target_language_is_left_unchanged()
    {
        var pipeline = new TranslationPipeline(new FakeCapturer(), new ScriptedDetector(true, false), new RussianOcr(), new TargetLanguageAwareTranslator(new UpperTranslator()));
        var region = new CaptureRegion(0, 0, 1, 1);

        await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);
        var outcome = await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);

        var line = Assert.IsType<PipelineOutcome.Translated>(outcome).Lines.Single();
        Assert.Equal("Привет, как дела", line.Translated);
    }

    [Fact]
    public async Task Unchanged_frame_never_runs_ocr()
    {
        var (pipeline, ocr) = Build(false, false);
        var region = new CaptureRegion(0, 0, 1, 1);
        await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);
        await pipeline.RunOnceAsync(region, LanguagePair.EnRu, default);
        Assert.Equal(0, ocr.Calls);
    }
}
