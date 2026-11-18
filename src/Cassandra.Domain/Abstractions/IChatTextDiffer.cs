using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface IChatTextDiffer
{
    /// <summary>Returns only lines not seen in the previous call. Remembers the current lines.</summary>
    IReadOnlyList<string> GetNewLines(IReadOnlyList<string> currentLines);

    void Reset();
}
