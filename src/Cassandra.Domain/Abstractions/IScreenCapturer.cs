using Cassandra.Domain.Models;
namespace Cassandra.Domain.Abstractions;

public interface IScreenCapturer
{
    Frame Capture(CaptureRegion region);
}
