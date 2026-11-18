using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

/// <summary>One pass: capture, detect change, wait for it to settle, OCR, diff, translate. Everything in memory.</summary>
public sealed class TranslationPipeline
{
    private readonly IScreenCapturer _capturer;
    private readonly IFrameChangeDetector _detector;
    private readonly IOcrEngine _ocr;
    private readonly IChatTextDiffer _differ;
    private readonly ITextTranslator _translator;
    private bool _pending;

    public TranslationPipeline(
        IScreenCapturer capturer,
        IFrameChangeDetector detector,
        IOcrEngine ocr,
        IChatTextDiffer differ,
        ITextTranslator translator)
    {
        _capturer = capturer;
        _detector = detector;
        _ocr = ocr;
        _differ = differ;
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
        var newLines = _differ.GetNewLines(recognized.Lines);
        if (newLines.Count == 0)
        {
            return new PipelineOutcome.NoNewText();
        }

        var result = new List<TranslatedLine>(newLines.Count);
        try
        {
            foreach (var line in newLines)
            {
                ct.ThrowIfCancellationRequested();
                var parsed = ChatLineParser.Parse(line);
                var translated = await _translator.TranslateAsync(parsed.Message, pair, ct).ConfigureAwait(false);
                result.Add(new TranslatedLine(line, translated, parsed.Speaker));
            }
        }
        catch
        {
            // Nothing was shown, so these lines must count as unseen: once translation works again (for example
            // after the player installs a model) they are recognized and translated instead of being lost.
            _differ.Reset();
            _pending = true;
            throw;
        }

        return new PipelineOutcome.Translated(result);
    }

    /// <summary>Forget the previous frame only. Already seen chat lines stay known, so nudging the frame does not repeat them.</summary>
    public void ResetFrame()
    {
        _pending = false;
        _detector.Reset();
    }

    /// <summary>Forget everything, e.g. after the language pair changed.</summary>
    public void Reset()
    {
        ResetFrame();
        _differ.Reset();
    }
}
