using Cassandra.Application.Detection;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class FrameChangeDetectorTests
{
    private static Frame Solid(byte value, int w = 128, int h = 32)
    {
        var px = new byte[w * h * 4];
        Array.Fill(px, value);
        return new Frame(w, h, px);
    }

    [Fact]
    public void First_frame_counts_as_changed()
    {
        Assert.True(new FrameChangeDetector().HasChanged(Solid(10)));
    }

    [Fact]
    public void Identical_frame_is_not_changed()
    {
        var d = new FrameChangeDetector();
        d.HasChanged(Solid(10));
        Assert.False(d.HasChanged(Solid(10)));
    }

    [Fact]
    public void Small_noise_is_ignored()
    {
        var d = new FrameChangeDetector();
        d.HasChanged(Solid(100));
        Assert.False(d.HasChanged(Solid(105)));
    }

    [Fact]
    public void Brightness_jump_is_a_change()
    {
        var d = new FrameChangeDetector();
        d.HasChanged(Solid(10));
        Assert.True(d.HasChanged(Solid(200)));
    }

    [Fact]
    public void New_text_in_one_row_is_a_change()
    {
        var d = new FrameChangeDetector();
        var before = Solid(10);
        d.HasChanged(before);

        var after = Solid(10);
        for (var x = 0; x < 128; x++)
        {
            var o = (20 * 128 + x) * 4;
            after.Pixels[o] = after.Pixels[o + 1] = after.Pixels[o + 2] = 255;
        }

        Assert.True(d.HasChanged(after));
    }

    [Fact]
    public void Reset_makes_next_frame_changed()
    {
        var d = new FrameChangeDetector();
        d.HasChanged(Solid(10));
        d.Reset();
        Assert.True(d.HasChanged(Solid(10)));
    }
}
