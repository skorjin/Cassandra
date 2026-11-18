using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Ocr;

/// <summary>Grayscale image prepared for Tesseract: dark text on light background, upscaled, contrast stretched.</summary>
public sealed record GrayImage(int Width, int Height, byte[] Pixels);

public static class ImagePreprocessor
{
    public static GrayImage Prepare(Frame frame, int scale = 2)
    {
        var gray = ToGray(frame);
        Stretch(gray);

        // Tesseract expects dark text on a light background; game chats are usually the opposite.
        if (Mean(gray) < 128)
        {
            for (var i = 0; i < gray.Length; i++)
            {
                gray[i] = (byte)(255 - gray[i]);
            }
        }

        return scale <= 1 ? new GrayImage(frame.Width, frame.Height, gray) : Upscale(gray, frame.Width, frame.Height, scale);
    }

    private static byte[] ToGray(Frame frame)
    {
        var gray = new byte[frame.Width * frame.Height];
        var p = frame.Pixels;
        for (var i = 0; i < gray.Length; i++)
        {
            var o = i * 4;
            gray[i] = (byte)((p[o] * 29 + p[o + 1] * 150 + p[o + 2] * 77) >> 8);
        }

        return gray;
    }

    private static void Stretch(byte[] gray)
    {
        if (gray.Length == 0)
        {
            return;
        }

        var histogram = new int[256];
        foreach (var v in gray)
        {
            histogram[v]++;
        }

        int Percentile(double fraction)
        {
            var target = (long)(gray.Length * fraction);
            long acc = 0;
            for (var v = 0; v < 256; v++)
            {
                acc += histogram[v];
                if (acc >= target)
                {
                    return v;
                }
            }

            return 255;
        }

        var lo = Percentile(0.01);
        var hi = Percentile(0.99);
        if (hi - lo < 16)
        {
            return;
        }

        for (var i = 0; i < gray.Length; i++)
        {
            gray[i] = (byte)Math.Clamp((gray[i] - lo) * 255 / (hi - lo), 0, 255);
        }
    }

    private static double Mean(byte[] gray)
    {
        long sum = 0;
        foreach (var v in gray)
        {
            sum += v;
        }

        return gray.Length == 0 ? 255 : (double)sum / gray.Length;
    }

    private static GrayImage Upscale(byte[] src, int w, int h, int scale)
    {
        var nw = w * scale;
        var nh = h * scale;
        var dst = new byte[nw * nh];
        for (var y = 0; y < nh; y++)
        {
            var fy = (y + 0.5) / scale - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(fy), 0, h - 1);
            var y1 = Math.Min(y0 + 1, h - 1);
            var ty = Math.Clamp(fy - y0, 0, 1);
            for (var x = 0; x < nw; x++)
            {
                var fx = (x + 0.5) / scale - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(fx), 0, w - 1);
                var x1 = Math.Min(x0 + 1, w - 1);
                var tx = Math.Clamp(fx - x0, 0, 1);
                var top = src[y0 * w + x0] * (1 - tx) + src[y0 * w + x1] * tx;
                var bottom = src[y1 * w + x0] * (1 - tx) + src[y1 * w + x1] * tx;
                dst[y * nw + x] = (byte)(top * (1 - ty) + bottom * ty + 0.5);
            }
        }

        return new GrayImage(nw, nh, dst);
    }
}
