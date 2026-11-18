using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Interop;
using static Cassandra.Infrastructure.Interop.NativeMethods;

namespace Cassandra.Infrastructure.Capture;

/// <summary>Copies a screen rectangle into a managed BGRA buffer through GDI. Nothing touches the disk.</summary>
public sealed class GdiScreenCapturer : IScreenCapturer
{
    public Frame Capture(CaptureRegion region)
    {
        if (region.IsEmpty)
        {
            throw new ArgumentException("Capture region is empty.", nameof(region));
        }

        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, region.Width, region.Height);
        var previous = SelectObject(memDc, bitmap);

        try
        {
            if (!BitBlt(memDc, 0, 0, region.Width, region.Height, screenDc, region.X, region.Y, SRCCOPY | CAPTUREBLT))
            {
                throw new InvalidOperationException("BitBlt failed: screen region could not be captured.");
            }

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = region.Width,
                biHeight = -region.Height, // negative: top-down rows
                biPlanes = 1,
                biBitCount = 32,
            };

            var pixels = new byte[region.Width * region.Height * 4];
            if (GetDIBits(memDc, bitmap, 0, (uint)region.Height, pixels, ref header, DIB_RGB_COLORS) == 0)
            {
                throw new InvalidOperationException("GetDIBits failed.");
            }

            return new Frame(region.Width, region.Height, pixels);
        }
        finally
        {
            SelectObject(memDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
