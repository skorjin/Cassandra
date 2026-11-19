using CommunityToolkit.Mvvm.ComponentModel;

namespace Cassandra.Presentation.State;

/// <summary>Overlay-wide flags shared by the HUD and the capture frame.</summary>
public sealed partial class OverlayState : ObservableObject
{
    /// <summary>Clicks pass through to the game, the capture frame is hidden.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>The player unlocked the capture frame with the HUD button or Ctrl+Alt+E. Pausing does not affect it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditFrame))]
    private bool _isFrameEditMode;

    /// <summary>The capture frame can be moved and resized. Otherwise clicks pass through it to the game.</summary>
    public bool CanEditFrame => IsFrameEditMode;

    /// <summary>The player asked to put both windows back on the primary monitor.</summary>
    public event Action? LayoutResetRequested;

    public void RequestLayoutReset()
    {
        IsVisible = true;
        LayoutResetRequested?.Invoke();
    }

    /// <summary>HUD and capture frame are shown.</summary>
    [ObservableProperty]
    private bool _isVisible = true;
}
