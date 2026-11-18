using Cassandra.Domain.Abstractions;
using static Cassandra.Infrastructure.Interop.NativeMethods;

namespace Cassandra.Infrastructure.Windows;

public sealed class Win32ForegroundWindowService : IForegroundWindowService
{
    private IntPtr _remembered;

    public void Remember()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != (uint)Environment.ProcessId)
        {
            _remembered = hwnd; // never remember our own overlay as "the game"
        }
    }

    public void Restore()
    {
        if (_remembered != IntPtr.Zero && IsWindow(_remembered))
        {
            SetForegroundWindow(_remembered);
        }
    }
}
