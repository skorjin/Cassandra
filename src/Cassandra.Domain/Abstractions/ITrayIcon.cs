namespace Cassandra.Domain.Abstractions;

/// <summary>One entry of the tray menu. A separator has no text and no action.</summary>
public sealed record TrayMenuItem(string Text, Action? Invoke = null, bool IsChecked = false, bool IsSeparator = false)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null, false, true);
}

/// <summary>Icon in the notification area. The menu is rebuilt every time it opens, so it always reflects the current state.</summary>
public interface ITrayIcon : IDisposable
{
    void Show(string tooltip, Func<IReadOnlyList<TrayMenuItem>> menu, Action onDoubleClick);
}
