using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Cassandra.Infrastructure.Translation;

/// <summary>One translation direction: SentencePiece tokenizer + ONNX encoder/decoder with greedy decoding.</summary>
internal sealed class MarianModel : IDisposable
{
    private const string SpaceMark = "▁";

    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _pieces;
    private readonly int _eos;
    private readonly int _pad;
    private readonly int _unk;
    private readonly int _maxLength;
    private readonly object _gate = new();

    public MarianModel(string directory)
    {
        var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "config.json"))).RootElement;
        _eos = config.GetProperty("eos_token_id").GetInt32();
        _pad = config.GetProperty("pad_token_id").GetInt32();
        _maxLength = Math.Min(config.GetProperty("max_length").GetInt32(), 256);

        _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(directory, "vocab.json")))!;
        _unk = _vocab.TryGetValue("<unk>", out var unk) ? unk : 1;
        _pieces = _vocab.ToDictionary(kv => kv.Value, kv => kv.Key);

        using var spm = File.OpenRead(Path.Combine(directory, "source.spm"));
        _tokenizer = SentencePieceTokenizer.Create(spm, addBeginningOfSentence: false, addEndOfSentence: false);

        var options = new SessionOptions
        {
            // leave CPU for the game
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 4, 1, 4),
            InterOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            EnableCpuMemArena = false,
            EnableMemoryPattern = false,
        };
        _encoder = new InferenceSession(Path.Combine(directory, "encoder_model.onnx"), options);
        _decoder = new InferenceSession(Path.Combine(directory, "decoder_model.onnx"), options);
    }

    public string Translate(string text, CancellationToken ct)
    {
        lock (_gate)
        {
            var source = Encode(text);
            var generated = Generate(source, ct);
            return Decode(generated);
        }
    }

    private long[] Encode(string text)
    {
        var ids = new List<long>();
        foreach (var token in _tokenizer.EncodeToTokens(text, out _))
        {
            ids.Add(_vocab.TryGetValue(token.Value, out var id) ? id : _unk);
        }

        ids.Add(_eos);
        return ids.ToArray();
    }

    private List<long> Generate(long[] source, CancellationToken ct)
    {
        var n = source.Length;
        var inputIds = new DenseTensor<long>(source, [1, n]);
        var mask = new DenseTensor<long>(Enumerable.Repeat(1L, n).ToArray(), [1, n]);

        using var encoderOut = _encoder.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        ]);
        var hidden = encoderOut.First(o => o.Name == "last_hidden_state").AsTensor<float>();

        var maxSteps = Math.Min(_maxLength, n * 3 + 10);
        var output = new List<long> { _pad };

        for (var step = 0; step < maxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();

            var decoderIds = new DenseTensor<long>(output.ToArray(), [1, output.Count]);
            using var result = _decoder.Run(
            [
                NamedOnnxValue.CreateFromTensor("input_ids", decoderIds),
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", mask),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hidden),
            ]);

            var logits = result.First(o => o.Name == "logits").AsTensor<float>();
            var last = output.Count - 1;
            var vocabSize = logits.Dimensions[2];

            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var v = 0; v < vocabSize; v++)
            {
                if (v == _pad)
                {
                    continue; // bad_words_ids
                }

                var score = logits[0, last, v];
                if (score > bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }

            if (best == _eos)
            {
                break;
            }

            output.Add(best);
            if (IsStuckInLoop(output))
            {
                break;
            }
        }

        output.RemoveAt(0);
        return output;
    }

    private static bool IsStuckInLoop(List<long> ids)
    {
        if (ids.Count < 9)
        {
            return false;
        }

        // the same 3-token phrase three times in a row
        for (var k = 1; k <= 3; k++)
        {
            var same = true;
            for (var i = 1; i < 3 * k && same; i++)
            {
                same = ids[^i] == ids[^(i + k)];
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }

    private string Decode(List<long> ids)
    {
        var sb = new StringBuilder();
        foreach (var id in ids)
        {
            if (id == _eos || id == _pad || id == _unk)
            {
                continue;
            }

            if (_pieces.TryGetValue((int)id, out var piece))
            {
                sb.Append(piece);
            }
        }

        return sb.Replace(SpaceMark, " ").ToString().Trim();
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
