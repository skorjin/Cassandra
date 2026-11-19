namespace Cassandra.Domain.Abstractions;

public enum HotkeyAction
{
    TogglePause,
    ToggleVisibility,
    ToggleLock,
    OpenReply,
    ToggleFrameEdit,
}

public interface IHotkeyService : IDisposable
{
    event Action<HotkeyAction>? HotkeyPressed;
    void RegisterAll();
}
