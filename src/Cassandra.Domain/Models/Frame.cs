namespace Cassandra.Domain.Models;

/// <summary>Raw captured image, BGRA32, stride = Width * 4. Lives only in memory.</summary>
public sealed record Frame(int Width, int Height, byte[] Pixels);
