namespace Cassandra.Infrastructure.Ocr;

/// <summary>Encodes an 8-bit grayscale image as BMP in memory, so Tesseract can load it without any file.</summary>
public static class BmpEncoder
{
    public static byte[] EncodeGray8(GrayImage image)
    {
        var stride = (image.Width + 3) & ~3;
        var pixelBytes = stride * image.Height;
        const int paletteBytes = 256 * 4;
        const int headerBytes = 14 + 40;
        var total = headerBytes + paletteBytes + pixelBytes;

        using var ms = new MemoryStream(total);
        using var w = new BinaryWriter(ms);

        w.Write((byte)'B');
        w.Write((byte)'M');
        w.Write(total);
        w.Write(0);
        w.Write(headerBytes + paletteBytes);

        w.Write(40);
        w.Write(image.Width);
        w.Write(image.Height); // positive: bottom-up
        w.Write((short)1);
        w.Write((short)8);
        w.Write(0);
        w.Write(pixelBytes);
        w.Write(2835);
        w.Write(2835);
        w.Write(256);
        w.Write(0);

        for (var i = 0; i < 256; i++)
        {
            w.Write((byte)i);
            w.Write((byte)i);
            w.Write((byte)i);
            w.Write((byte)0);
        }

        var padding = new byte[stride - image.Width];
        for (var y = image.Height - 1; y >= 0; y--)
        {
            w.Write(image.Pixels, y * image.Width, image.Width);
            w.Write(padding);
        }

        return ms.ToArray();
    }
}
