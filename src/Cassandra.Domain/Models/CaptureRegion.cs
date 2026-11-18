namespace Cassandra.Domain.Models;

/// <summary>Screen rectangle in physical pixels.</summary>
public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
