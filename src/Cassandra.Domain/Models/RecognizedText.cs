namespace Cassandra.Domain.Models;

public sealed record RecognizedText(IReadOnlyList<string> Lines)
{
    public static RecognizedText Empty { get; } = new(Array.Empty<string>());

    public bool IsEmpty => Lines.Count == 0;
}
