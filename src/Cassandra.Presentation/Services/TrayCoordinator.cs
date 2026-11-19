using Cassandra.Domain.Abstractions;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Services;

/// <summary>
/// Tray icon: the way back to an overlay that is hidden or lost behind the game. The window itself has no taskbar button
/// on purpose (it would show up in Alt+Tab and could take focus from the game).
/// </summary>
public sealed class TrayCoordinator : IDisposable
{
    private readonly ITrayIcon _tray;
    private readonly HudViewModel _hud;
    private readonly OverlayState _overlay;

    public TrayCoordinator(ITrayIcon tray, HudViewModel hud, OverlayState overlay)
    {
        _tray = tray;
        _hud = hud;
        _overlay = overlay;
    }

    public void Start() => _tray.Show("Cassandra", BuildMenu, ToggleVisibility);

    internal IReadOnlyList<TrayMenuItem> BuildMenu() =>
    [
        new(_overlay.IsVisible ? "Скрыть окно" : "Показать окно", ToggleVisibility),
        new(_hud.IsPaused ? "Продолжить перевод" : "Пауза", () => _hud.TogglePauseCommand.Execute(null)),
        new("Двигать рамку захвата", () => _hud.ToggleFrameEditCommand.Execute(null), _overlay.IsFrameEditMode),
        new("Настройки", OpenSettings),
        TrayMenuItem.Separator,
        new("Выход", () => _hud.CloseCommand.Execute(null)),
    ];

    private void ToggleVisibility() => _overlay.IsVisible = !_overlay.IsVisible;

    private void OpenSettings()
    {
        _overlay.IsVisible = true;
        if (!_hud.IsSettingsOpen)
        {
            _hud.ToggleSettingsCommand.Execute(null);
        }
    }

    public void Dispose() => _tray.Dispose();
}
