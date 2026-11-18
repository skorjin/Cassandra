using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Detection;

/// <summary>
/// Cheap change detection: the frame is reduced to a coarse grid of average luminance values
/// and compared with the previous grid. Only a few KB are kept and no OCR is needed for the check.
/// </summary>
public sealed class FrameChangeDetector : IFrameChangeDetector
{
    private const int GridWidth = 64;
    private const int GridHeight = 16;

    private readonly int _cellTolerance;
    private readonly double _changedCellsRatio;
    private byte[]? _previous;

    /// <param name="cellTolerance">Luminance delta (0-255) a cell must exceed to count as changed.</param>
    /// <param name="changedCellsRatio">Share of changed cells that makes the frame "changed".</param>
    public FrameChangeDetector(int cellTolerance = 12, double changedCellsRatio = 0.01)
    {
        _cellTolerance = cellTolerance;
        _changedCellsRatio = changedCellsRatio;
    }

    public bool HasChanged(Frame frame)
    {
        var grid = BuildGrid(frame);
        var previous = _previous;
        _previous = grid;
        if (previous is null)
        {
            return true;
        }

        var changed = 0;
        for (var i = 0; i < grid.Length; i++)
        {
            if (Math.Abs(grid[i] - previous[i]) > _cellTolerance)
            {
                changed++;
            }
        }

        return changed >= Math.Max(1, (int)(grid.Length * _changedCellsRatio));
    }

    public void Reset() => _previous = null;

    private static byte[] BuildGrid(Frame frame)
    {
        var grid = new byte[GridWidth * GridHeight];
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return grid;
        }

        var sums = new long[grid.Length];
        var counts = new int[grid.Length];
        var pixels = frame.Pixels;

        for (var y = 0; y < frame.Height; y++)
        {
            var gy = y * GridHeight / frame.Height;
            var row = y * frame.Width * 4;
            for (var x = 0; x < frame.Width; x++)
            {
                var o = row + x * 4;
                // integer luma approximation, BGRA byte order
                var luma = (pixels[o] * 29 + pixels[o + 1] * 150 + pixels[o + 2] * 77) >> 8;
                var cell = gy * GridWidth + x * GridWidth / frame.Width;
                sums[cell] += luma;
                counts[cell]++;
            }
        }

        for (var i = 0; i < grid.Length; i++)
        {
            grid[i] = counts[i] == 0 ? (byte)0 : (byte)(sums[i] / counts[i]);
        }

        return grid;
    }
}
