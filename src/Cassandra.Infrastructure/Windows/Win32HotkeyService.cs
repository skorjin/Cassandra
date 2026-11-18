using Cassandra.Domain.Abstractions;
using static Cassandra.Infrastructure.Interop.NativeMethods;

namespace Cassandra.Infrastructure.Windows;

/// <summary>Global hotkeys on a dedicated thread with its own message loop (no window needed).</summary>
public sealed class Win32HotkeyService : IHotkeyService
{
    private static readonly (HotkeyAction Action, uint Key)[] Bindings =
    [
        (HotkeyAction.TogglePause, 'P'),
        (HotkeyAction.ToggleVisibility, 'H'),
        (HotkeyAction.ToggleLock, 'L'),
        (HotkeyAction.OpenReply, 'R'),
    ];

    private Thread? _thread;
    private uint _threadId;

    public event Action<HotkeyAction>? HotkeyPressed;

    /// <summary>Hotkeys that another application already owns. Empty when everything registered.</summary>
    public IReadOnlyList<HotkeyAction> Failed { get; private set; } = Array.Empty<HotkeyAction>();

    public void RegisterAll()
    {
        if (_thread is not null)
        {
            return;
        }

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Loop(ready)) { IsBackground = true, Name = "Hotkeys" };
        _thread.Start();
        ready.Wait();
    }

    private void Loop(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        var failed = new List<HotkeyAction>();
        for (var i = 0; i < Bindings.Length; i++)
        {
            if (!RegisterHotKey(IntPtr.Zero, i + 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, Bindings[i].Key))
            {
                failed.Add(Bindings[i].Action);
            }
        }

        Failed = failed;
        ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                var index = (int)msg.wParam - 1;
                if (index >= 0 && index < Bindings.Length)
                {
                    HotkeyPressed?.Invoke(Bindings[index].Action);
                }
            }
        }

        for (var i = 0; i < Bindings.Length; i++)
        {
            UnregisterHotKey(IntPtr.Zero, i + 1);
        }
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            return;
        }

        PostThreadMessage(_threadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
    }
}
