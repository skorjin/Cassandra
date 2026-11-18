using CommunityToolkit.Mvvm.ComponentModel;

namespace Cassandra.Presentation.State;

/// <summary>Overlay-wide flags shared by the HUD and the capture frame.</summary>
public sealed partial class OverlayState : ObservableObject
{
    /// <summary>Clicks pass through to the game, the capture frame is hidden.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>HUD and capture frame are shown.</summary>
    [ObservableProperty]
    private bool _isVisible = true;
}
