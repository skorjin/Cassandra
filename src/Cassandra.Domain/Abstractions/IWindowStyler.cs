namespace Cassandra.Domain.Abstractions;

/// <summary>Native window behaviour that WPF does not expose: click-through, focus, capture exclusion.</summary>
public interface IWindowStyler
{
    /// <summary>Mouse clicks fall through to the window below (the game).</summary>
    void SetClickThrough(IntPtr hwnd, bool enabled);

    /// <summary>When false the window never takes focus from the game; set true while the player types.</summary>
    void SetActivatable(IntPtr hwnd, bool activatable);

    /// <summary>Hide the window from screenshots so OCR never sees our own overlay.</summary>
    void ExcludeFromCapture(IntPtr hwnd);

    /// <summary>Takes keyboard focus even when another application (the game) is in the foreground.</summary>
    void BringToForeground(IntPtr hwnd);

    /// <summary>Window rectangle in physical screen pixels, immune to per-monitor DPI rounding in WPF.</summary>
    Cassandra.Domain.Models.CaptureRegion GetBounds(IntPtr hwnd);

    void SetBounds(IntPtr hwnd, Cassandra.Domain.Models.CaptureRegion bounds);

    /// <summary>Remove the window from Alt-Tab.</summary>
    void HideFromTaskSwitcher(IntPtr hwnd);
}
