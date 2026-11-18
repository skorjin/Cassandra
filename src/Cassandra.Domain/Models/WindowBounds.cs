namespace Cassandra.Domain.Models;

/// <summary>Window position and size in device-independent units.</summary>
public readonly record struct WindowBounds(double Left, double Top, double Width, double Height);
