using Cassandra.Domain.Models;

namespace Cassandra.Presentation.Services;

/// <summary>
/// Keeps the overlay windows reachable. A position saved on a monitor that is no longer connected would put a window
/// where nobody can see or grab it, so such a window is moved onto the primary monitor.
/// </summary>
public static class WindowPlacement
{
    private const int MinGrabWidth = 96;
    private const int MinGrabHeight = 32;
    private const int Margin = 60;

    /// <summary>True when enough of the window lies on a connected monitor to see and drag it.</summary>
    public static bool IsReachable(CaptureRegion window, IReadOnlyList<CaptureRegion> workAreas) =>
        workAreas.Any(area =>
        {
            var width = Math.Min(window.X + window.Width, area.X + area.Width) - Math.Max(window.X, area.X);
            var height = Math.Min(window.Y + window.Height, area.Y + area.Height) - Math.Max(window.Y, area.Y);
            return width >= Math.Min(window.Width, MinGrabWidth) && height >= Math.Min(window.Height, MinGrabHeight);
        });

    /// <summary>Top-left corner of the primary monitor; the size is kept unless it would not fit.</summary>
    public static CaptureRegion HudDefault(CaptureRegion current, CaptureRegion primary) =>
        Fit(current, primary, primary.X + Margin, primary.Y + Margin);

    /// <summary>Lower half of the primary monitor, where game chats usually are.</summary>
    public static CaptureRegion FrameDefault(CaptureRegion current, CaptureRegion primary) =>
        Fit(current, primary, primary.X + Margin, primary.Y + primary.Height * 55 / 100);

    private static CaptureRegion Fit(CaptureRegion size, CaptureRegion area, int x, int y)
    {
        var width = Math.Min(size.Width, area.Width - 2 * Margin);
        var height = Math.Min(size.Height, area.Height - 2 * Margin);
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.Width - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        return new CaptureRegion(x, y, width, height);
    }
}
