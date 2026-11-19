using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

/// <summary>
/// One pass: capture, detect change, wait for it to settle, OCR, translate. The result is exactly what the frame shows
/// right now. The only shortcut is the change detector: a static chat is not recognized and translated again. All in memory.
/// </summary>
public sealed class TranslationPipeline
{
    private readonly IScreenCapturer _capturer;
    private readonly IFrameChangeDetector _detector;
    private readonly IOcrEngine _ocr;
    private readonly ITextTranslator _translator;
    private bool _pending;

    public TranslationPipeline(
        IScreenCapturer capturer,
        IFrameChangeDetector detector,
        IOcrEngine ocr,
        ITextTranslator translator)
    {
        _capturer = capturer;
        _detector = detector;
        _ocr = ocr;
        _translator = translator;
    }

    public async Task<PipelineOutcome> RunOnceAsync(CaptureRegion region, LanguagePair pair, CancellationToken ct)
    {
        var frame = _capturer.Capture(region);

        if (_detector.HasChanged(frame))
        {
            _pending = true;
            return new PipelineOutcome.Settling();
        }

        if (!_pending)
        {
            return new PipelineOutcome.Idle();
        }

        _pending = false;

        var recognized = await _ocr.RecognizeAsync(frame, pair.Source, ct).ConfigureAwait(false);
        var snapshot = new List<TranslatedLine>(recognized.Lines.Count);
        try
        {
            foreach (var line in recognized.Lines.Where(l => l.Any(char.IsLetterOrDigit)))
            {
                ct.ThrowIfCancellationRequested();
                var parsed = ChatLineParser.Parse(line);
                var translated = await _translator.TranslateAsync(parsed.Message, pair, ct).ConfigureAwait(false);
                snapshot.Add(new TranslatedLine(line, translated, parsed.Speaker));
            }
        }
        catch
        {
            // Nothing was shown, so the frame must be processed again (for example once a model is installed).
            _pending = true;
            throw;
        }

        return new PipelineOutcome.Translated(snapshot);
    }

    /// <summary>Forget the previous frame, e.g. after the capture frame moved.</summary>
    public void ResetFrame()
    {
        _pending = false;
        _detector.Reset();
    }

    /// <summary>Forget the previous frame after the language pair changed.</summary>
    public void Reset() => ResetFrame();
}
