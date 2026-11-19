using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Ocr;

/// <summary>Grayscale image prepared for Tesseract: dark text on light background, upscaled, contrast stretched.</summary>
public sealed record GrayImage(int Width, int Height, byte[] Pixels);

public static class ImagePreprocessor
{
    /// <summary>Text line height (ascender to descender, in pixels) Tesseract reads most reliably.</summary>
    private const int TargetLineHeight = 56;
    private const double FallbackScale = 3;
    private const double MaxScale = 4;
    private const int MaxOutputWidth = 3200;
    private const int MaxOutputHeight = 2000;

    /// <param name="scale">Upscale factor; 0 picks one from the measured text size so small chat fonts get enlarged more.</param>
    public static GrayImage Prepare(Frame frame, double scale = 0)
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

        if (scale <= 0)
        {
            scale = ChooseScale(gray, frame.Width, frame.Height);
        }

        return scale <= 1 ? new GrayImage(frame.Width, frame.Height, gray) : Upscale(gray, frame.Width, frame.Height, scale);
    }

    /// <summary>Scale that brings the typical text line to about <see cref="TargetLineHeight"/> pixels, within output size limits.</summary>
    public static double ChooseScale(byte[] darkTextOnLight, int width, int height)
    {
        var lineHeight = EstimateLineHeight(darkTextOnLight, width, height);
        var scale = lineHeight <= 0 ? FallbackScale : Math.Round(2.0 * TargetLineHeight / lineHeight) / 2; // half-step factors
        scale = Math.Clamp(scale, 1.0, MaxScale);

        while (scale > 1 && (width * scale > MaxOutputWidth || height * scale > MaxOutputHeight))
        {
            scale -= 0.5;
        }

        return scale;
    }

    /// <summary>
    /// Median height of the text bands (runs of rows that contain dark pixels), or 0 when no reliable
    /// measurement is possible. The input must already be dark text on a light background.
    /// </summary>
    public static int EstimateLineHeight(byte[] gray, int width, int height)
    {
        const byte inkBelow = 110;
        var minInkPerRow = Math.Max(2, width / 200);

        var bands = new List<int>();
        var runStart = -1;
        var gap = 0;
        for (var y = 0; y <= height; y++)
        {
            var inked = false;
            if (y < height)
            {
                var count = 0;
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (gray[row + x] < inkBelow)
                    {
                        count++;
                    }
                }

                inked = count >= minInkPerRow;
            }

            if (inked)
            {
                if (runStart < 0)
                {
                    runStart = y;
                }

                gap = 0;
            }
            else if (runStart >= 0 && (++gap > 1 || y == height))
            {
                var length = y - gap - runStart + 1;
                if (length >= 4)
                {
                    bands.Add(length);
                }

                runStart = -1;
                gap = 0;
            }
        }

        if (bands.Count == 0)
        {
            return 0;
        }

        bands.Sort();
        var median = bands[bands.Count / 2];
        return median > 70 ? 0 : median; // lines fused into one block: the measurement means nothing
    }

    private static byte[] ToGray(Frame frame)
    {
        var gray = new byte[frame.Width * frame.Height];
        var p = frame.Pixels;
        for (var i = 0; i < gray.Length; i++)
        {
            var o = i * 4;
            // Brightest channel rather than luminance: saturated red and blue chat colours (quest links, channel
            // names) have low luminance and would melt into the dark background.
            gray[i] = Math.Max(p[o], Math.Max(p[o + 1], p[o + 2]));
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

    private static GrayImage Upscale(byte[] src, int w, int h, double scale)
    {
        var nw = (int)Math.Round(w * scale);
        var nh = (int)Math.Round(h * scale);
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
