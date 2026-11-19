using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Tesseract;

namespace Cassandra.Infrastructure.Ocr;

/// <summary>Tesseract OCR. Frames are preprocessed and handed over as in-memory BMP; tessdata is read-only.</summary>
public sealed class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private static readonly Dictionary<string, string> LanguageCodes = new()
    {
        ["en"] = "eng",
        ["ru"] = "rus",
    };

    private const float MinLineConfidence = 45f;

    private readonly string _tessdataPath;
    private readonly Dictionary<string, TesseractEngine> _engines = new();
    private readonly object _gate = new();

    public TesseractOcrEngine(string? tessdataPath = null) => _tessdataPath = tessdataPath ?? AppPaths.Tessdata;

    public Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct) =>
        Task.Run(() => Recognize(frame, language, ct), ct);

    private RecognizedText Recognize(Frame frame, string language, CancellationToken ct)
    {
        var bmp = BmpEncoder.EncodeGray8(ImagePreprocessor.Prepare(frame));

        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            var engine = GetEngine(language);
            using var pix = Pix.LoadFromMemory(bmp);
            using var page = engine.Process(pix, PageSegMode.SingleBlock);
            return new RecognizedText(ReadConfidentLines(page));
        }
    }

    /// <summary>Text lines whose mean confidence is acceptable; drops specks and UI artifacts that OCR mistakes for text.</summary>
    private static IReadOnlyList<string> ReadConfidentLines(Page page)
    {
        var lines = new List<string>();
        using var iterator = page.GetIterator();
        iterator.Begin();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (!string.IsNullOrEmpty(text) && iterator.GetConfidence(PageIteratorLevel.TextLine) >= MinLineConfidence)
            {
                lines.AddRange(SplitLines(text));
            }
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    private TesseractEngine GetEngine(string language)
    {
        if (!LanguageCodes.TryGetValue(language, out var code))
        {
            throw new NotSupportedException($"OCR language '{language}' is not supported.");
        }

        // Game chats mix scripts (English nicknames, Russian messages, addon notices), so every language is read
        // with both models at once; otherwise text in the "other" script comes out as garbage.
        const string combined = "eng+rus";
        if (_engines.TryGetValue(combined, out var engine))
        {
            return engine;
        }

        foreach (var part in LanguageCodes.Values.Append(code).Distinct())
        {
            if (!File.Exists(Path.Combine(_tessdataPath, part + ".traineddata")))
            {
                throw new FileNotFoundException(
                    $"Tesseract data '{part}.traineddata' not found in '{_tessdataPath}'.");
            }
        }

        engine = new TesseractEngine(_tessdataPath, combined, EngineMode.LstmOnly);
        _engines[combined] = engine;
        return engine;
    }

    internal static IReadOnlyList<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Count(char.IsLetter) >= 2)
            .ToList();

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var engine in _engines.Values)
            {
                engine.Dispose();
            }

            _engines.Clear();
        }
    }
}
