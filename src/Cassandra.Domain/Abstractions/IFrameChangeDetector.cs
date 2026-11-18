using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface IFrameChangeDetector
{
    /// <summary>True when the frame differs noticeably from the previous one. Remembers the frame.</summary>
    bool HasChanged(Frame frame);

    void Reset();
}
