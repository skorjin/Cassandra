using Cassandra.Application.Detection;
using Cassandra.Application.Pipeline;
using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class CaptureLoopTests
{
    private sealed class FakeCapturer : IScreenCapturer
    {
        private int _n;
        public Frame Capture(CaptureRegion region)
        {
            // first captures differ (chat arrived), later ones are stable
            var value = (byte)(_n++ < 1 ? 20 : 220);
            var px = new byte[64 * 16 * 4];
            Array.Fill(px, value);
            return new Frame(64, 16, px);
        }
    }

    private sealed class CountingOcr : IOcrEngine
    {
        public int Calls;
        public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new RecognizedText(new[] { "Nick: hello there" }));
        }
    }

    private sealed class EchoTranslator : ITextTranslator
    {
        public Task<string> TranslateAsync(string text, LanguagePair pair, CancellationToken ct) =>
            Task.FromResult("<" + text + ">");
    }

    private sealed class MemoryStore : ISettingsStore
    {
        public AppSettings Load() => new() { Capture = new CaptureRegion(0, 0, 64, 16), CaptureIntervalMs = 20 };
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Loop_translates_new_text_once_then_stops_on_pause()
    {
        var ocr = new CountingOcr();
        var settings = new SettingsService(new MemoryStore());
        var pipeline = new TranslationPipeline(new FakeCapturer(), new FrameChangeDetector(), ocr, new ChatTextDiffer(), new EchoTranslator());
        using var loop = new CaptureLoopService(pipeline, settings);

        var received = new List<TranslatedLine>();
        loop.LinesTranslated += lines => { lock (received) received.AddRange(lines); };

        loop.Start();
        await Task.Delay(500);
        await loop.PauseAsync();

        Assert.Equal(PipelineState.Paused, loop.State);
        lock (received)
        {
            var line = Assert.Single(received); // same OCR text on later passes must not repeat
            Assert.Equal("<hello there>", line.Translated);
            Assert.Equal("Nick", line.Speaker);
        }
    }

    [Fact]
    public async Task Moving_the_frame_does_not_repeat_known_lines()
    {
        var ocr = new CountingOcr();
        var settings = new SettingsService(new MemoryStore());
        var capturer = new FakeCapturer();
        var pipeline = new TranslationPipeline(capturer, new FrameChangeDetector(), ocr, new ChatTextDiffer(), new EchoTranslator());
        using var loop = new CaptureLoopService(pipeline, settings);

        var count = 0;
        loop.LinesTranslated += lines => Interlocked.Add(ref count, lines.Count);

        loop.Start();
        await Task.Delay(300);
        settings.Update(s => s with { Capture = new CaptureRegion(5, 5, 64, 16) });
        await Task.Delay(300);
        await loop.PauseAsync();

        Assert.Equal(1, count);
    }
}
