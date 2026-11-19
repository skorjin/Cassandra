using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Ocr;

namespace Cassandra.Infrastructure.Tests;

public class ImagePreprocessorColorTests
{
    [Fact]
    public void Saturated_red_text_on_a_dark_background_stays_distinct_from_it()
    {
        // BGRA: dark background pixels with one pure-red "text" pixel; red has low luminance but must stay visible
        var pixels = new byte[16 * 16 * 4];
        for (var i = 0; i < 16 * 16; i++)
        {
            pixels[i * 4] = 20;
            pixels[i * 4 + 1] = 20;
            pixels[i * 4 + 2] = 20;
            pixels[i * 4 + 3] = 255;
        }

        pixels[0] = 0;
        pixels[1] = 0;
        pixels[2] = 255;

        var gray = ImagePreprocessor.Prepare(new Frame(16, 16, pixels), scale: 1);

        // after inversion text is darker than the background by a wide margin
        Assert.True(gray.Pixels[1] - gray.Pixels[0] > 150);
    }
}

public class ImagePreprocessorScaleTests
{
    private static byte[] TextLikeBands(int width, int height, int bandHeight, int gap)
    {
        var gray = new byte[width * height];
        Array.Fill(gray, (byte)230);
        for (var top = 4; top + bandHeight < height; top += bandHeight + gap)
        {
            for (var y = top; y < top + bandHeight; y++)
            {
                for (var x = 10; x < width - 10; x++)
                {
                    gray[y * width + x] = 20;
                }
            }
        }

        return gray;
    }

    [Theory]
    [InlineData(20, 3.0)]
    [InlineData(28, 2.0)]
    [InlineData(14, 4.0)]
    [InlineData(60, 1.0)]
    public void Smaller_text_gets_a_bigger_scale(int bandHeight, double expected)
    {
        var gray = TextLikeBands(400, 240, bandHeight, 6);
        Assert.Equal(bandHeight, ImagePreprocessor.EstimateLineHeight(gray, 400, 240));
        Assert.Equal(expected, ImagePreprocessor.ChooseScale(gray, 400, 240));
    }

    [Fact]
    public void Unmeasurable_frame_falls_back_to_the_default_scale()
    {
        var allWhite = new byte[400 * 100];
        Array.Fill(allWhite, (byte)255);
        Assert.Equal(3.0, ImagePreprocessor.ChooseScale(allWhite, 400, 100));
    }

    [Fact]
    public void Wide_frames_do_not_blow_up_the_output_size()
    {
        var gray = TextLikeBands(2400, 200, 14, 6);
        Assert.True(2400 * ImagePreprocessor.ChooseScale(gray, 2400, 200) <= 3200);
    }
}
