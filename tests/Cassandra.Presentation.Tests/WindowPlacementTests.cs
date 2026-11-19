using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;

namespace Cassandra.Presentation.Tests;

public class WindowPlacementTests
{
    private static readonly CaptureRegion Primary = new(0, 0, 2560, 1400);
    private static readonly CaptureRegion Left = new(-1920, 0, 1920, 1040);

    [Theory]
    [InlineData(100, 100, true)]            // on the primary monitor
    [InlineData(2400, 600, true)]           // hangs over the right edge but a part is visible
    [InlineData(-1800, 200, false)]         // on a second monitor that is not connected
    [InlineData(5000, 100, false)]          // far outside
    [InlineData(100, 1390, false)]          // only a sliver above the bottom edge
    public void Reachability_with_only_the_primary_monitor(int x, int y, bool expected) =>
        Assert.Equal(expected, WindowPlacement.IsReachable(new CaptureRegion(x, y, 500, 300), [Primary]));

    [Fact]
    public void A_window_on_a_connected_second_monitor_is_left_alone() =>
        Assert.True(WindowPlacement.IsReachable(new CaptureRegion(-1800, 200, 500, 300), [Primary, Left]));

    [Fact]
    public void Defaults_land_inside_the_primary_monitor_and_keep_the_size()
    {
        var hud = WindowPlacement.HudDefault(new CaptureRegion(-1800, 200, 533, 300), Primary);
        var frame = WindowPlacement.FrameDefault(new CaptureRegion(5000, 9000, 517, 216), Primary);

        Assert.Equal(new CaptureRegion(60, 60, 533, 300), hud);
        Assert.Equal(517, frame.Width);
        Assert.True(WindowPlacement.IsReachable(hud, [Primary]));
        Assert.True(WindowPlacement.IsReachable(frame, [Primary]));
        Assert.True(frame.Y > hud.Y + hud.Height); // the frame does not start on top of the HUD
    }

    [Fact]
    public void An_oversized_window_is_shrunk_to_fit_a_small_monitor()
    {
        var small = new CaptureRegion(0, 0, 800, 600);
        var fitted = WindowPlacement.HudDefault(new CaptureRegion(0, 0, 1500, 900), small);

        Assert.True(fitted.Width <= 800 && fitted.Height <= 600);
        Assert.True(WindowPlacement.IsReachable(fitted, [small]));
    }
}
