using Cassandra.Domain.Abstractions;
using static Cassandra.Infrastructure.Interop.NativeMethods;

namespace Cassandra.Infrastructure.Windows;

public sealed class Win32WindowStyler : IWindowStyler
{
    public void SetClickThrough(IntPtr hwnd, bool enabled) =>
        ModifyExStyle(hwnd, enabled ? WS_EX_TRANSPARENT | WS_EX_LAYERED : 0, enabled ? 0 : WS_EX_TRANSPARENT);

    public void SetActivatable(IntPtr hwnd, bool activatable) =>
        ModifyExStyle(hwnd, activatable ? 0 : WS_EX_NOACTIVATE, activatable ? WS_EX_NOACTIVATE : 0);

    public void ExcludeFromCapture(IntPtr hwnd)
    {
        // Debug switch: make the overlay visible to screenshots while developing.
        if (Environment.GetEnvironmentVariable("CASSANDRA_SHOW_IN_CAPTURE") == "1")
        {
            return;
        }

        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
    }

    public void BringToForeground(IntPtr hwnd)
    {
        // Windows only lets the foreground thread hand out focus; attach to its input queue for a moment.
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var ourThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ourThread &&
                       AttachThreadInput(ourThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(ourThread, foregroundThread, false);
            }
        }
    }

    public Cassandra.Domain.Models.CaptureRegion GetBounds(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        return new Cassandra.Domain.Models.CaptureRegion(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    public void SetBounds(IntPtr hwnd, Cassandra.Domain.Models.CaptureRegion b) =>
        SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SWP_NOZORDER | SWP_NOACTIVATE);

    public void HideFromTaskSwitcher(IntPtr hwnd) => ModifyExStyle(hwnd, WS_EX_TOOLWINDOW, 0);

    private static void ModifyExStyle(IntPtr hwnd, long add, long remove)
    {
        var current = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        var updated = (current | add) & ~remove;
        if (updated != current)
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(updated));
        }
    }
}
