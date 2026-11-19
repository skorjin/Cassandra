using Cassandra.Domain.Models;

namespace Cassandra.Domain.Abstractions;

/// <summary>The monitors connected right now, as work areas (without the taskbar) in physical pixels.</summary>
public interface IScreenLayout
{
    IReadOnlyList<CaptureRegion> WorkAreas { get; }

    CaptureRegion PrimaryWorkArea { get; }
}
