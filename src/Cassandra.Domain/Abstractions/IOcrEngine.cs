using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface IOcrEngine
{
    Task<RecognizedText> RecognizeAsync(Frame frame, string language, CancellationToken ct);
}
