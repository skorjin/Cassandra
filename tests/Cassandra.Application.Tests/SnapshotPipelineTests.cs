using Cassandra.Application.Detection;
using Cassandra.Application.Pipeline;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class SnapshotPipelineTests
{
    private sealed class StableCapturer : IScreenCapturer
    {
        public Frame Capture(CaptureRegion region) => new(8, 8, new byte[8 * 8 * 4]);
    }

    private sealed class SequenceOcr : IOcrEngine
    {
        private readonly Queue<string[]> _frames;
        public SequenceOcr(params string[][] frames) => _frames = new Queue<string[]>(frames);

        public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct) =>
            Task.FromResult(new RecognizedText(_frames.Dequeue()));
    }

    private sealed class CountingTranslator : ITextTranslator
    {
        public List<string> Calls { get; } = [];

        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct)
        {
            Calls.Add(text);
            return Task.FromResult("<" + text + ">");
        }
    }

    private static readonly CaptureRegion Region = new(0, 0, 8, 8);

    /// <summary>The first pass sees a changed frame, the second a stable one, so OCR runs on the second.</summary>
    private static async Task<PipelineOutcome> SettledPass(TranslationPipeline pipeline)
    {
        await pipeline.RunOnceAsync(Region, LanguagePair.EnRu, default);
        return await pipeline.RunOnceAsync(Region, LanguagePair.EnRu, default);
    }

    [Fact]
    public async Task Every_frame_is_recognized_and_translated_in_full()
    {
        var ocr = new SequenceOcr(["Amy: one", "Bob: two", "Cat: three"], ["Bob: two", "Cat: three", "Dan: four"]);
        var translator = new CountingTranslator();
        var pipeline = new TranslationPipeline(new StableCapturer(), new FrameChangeDetector(), ocr, translator);

        var first = Assert.IsType<PipelineOutcome.Translated>(await SettledPass(pipeline));
        Assert.Equal(["one", "two", "three"], first.Lines.Select(l => l.Translated.Trim('<', '>')));

        pipeline.ResetFrame();
        var second = Assert.IsType<PipelineOutcome.Translated>(await SettledPass(pipeline));
        Assert.Equal(["two", "three", "four"], second.Lines.Select(l => l.Translated.Trim('<', '>')));
        Assert.Equal(6, translator.Calls.Count); // no memory between frames: what is on screen is what gets translated
    }

    [Fact]
    public async Task A_frame_with_less_text_shows_less_text()
    {
        var ocr = new SequenceOcr(["Amy: one", "Bob: two", "Cat: three"], ["Cat: three"]);
        var pipeline = new TranslationPipeline(new StableCapturer(), new FrameChangeDetector(), ocr, new CountingTranslator());

        await SettledPass(pipeline);
        pipeline.ResetFrame();
        var outcome = Assert.IsType<PipelineOutcome.Translated>(await SettledPass(pipeline));

        Assert.Equal("three", Assert.Single(outcome.Lines).Translated.Trim('<', '>'));
    }

    [Fact]
    public async Task A_static_chat_is_translated_once_not_on_every_pass()
    {
        var ocr = new SequenceOcr(["Amy: one", "Bob: two"]); // a second OCR call would throw: the queue is empty
        var translator = new CountingTranslator();
        var pipeline = new TranslationPipeline(new StableCapturer(), new FrameChangeDetector(), ocr, translator);

        await SettledPass(pipeline);
        var idle = await pipeline.RunOnceAsync(Region, LanguagePair.EnRu, default);
        var idleAgain = await pipeline.RunOnceAsync(Region, LanguagePair.EnRu, default);

        Assert.IsType<PipelineOutcome.Idle>(idle);
        Assert.IsType<PipelineOutcome.Idle>(idleAgain);
        Assert.Equal(2, translator.Calls.Count);
    }
}