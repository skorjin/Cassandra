using Cassandra.Domain.Abstractions;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Services;

/// <summary>Maps global hotkeys to HUD commands and overlay flags on the UI thread.</summary>
public sealed class HotkeyCoordinator : IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly HudViewModel _hud;
    private readonly OverlayState _overlay;
    private readonly IUiDispatcher _ui;

    public HotkeyCoordinator(IHotkeyService hotkeys, HudViewModel hud, OverlayState overlay, IUiDispatcher ui)
    {
        _hotkeys = hotkeys;
        _hud = hud;
        _overlay = overlay;
        _ui = ui;
    }

    public void Start()
    {
        _hotkeys.HotkeyPressed += OnHotkey;
        _hotkeys.RegisterAll();
    }

    public void OnHotkey(HotkeyAction action) => _ui.Post(() =>
    {
        switch (action)
        {
            case HotkeyAction.TogglePause:
                _hud.TogglePauseCommand.Execute(null);
                break;
            case HotkeyAction.ToggleVisibility:
                _overlay.IsVisible = !_overlay.IsVisible;
                break;
            case HotkeyAction.ToggleLock:
                _overlay.IsLocked = !_overlay.IsLocked;
                break;
            case HotkeyAction.OpenReply:
                _overlay.IsVisible = true;
                _hud.OpenReplyCommand.Execute(null);
                break;
        }
    });

    public void Dispose()
    {
        _hotkeys.HotkeyPressed -= OnHotkey;
        _hotkeys.Dispose();
    }
}
