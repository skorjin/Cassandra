using Cassandra.Application.Settings;
using Cassandra.Domain.Models;
using Cassandra.Presentation.State;

namespace Cassandra.Presentation.ViewModels;

/// <summary>Position and size of the capture frame, kept in settings in physical screen pixels.</summary>
public sealed class CaptureFrameViewModel
{
    private readonly ISettingsService _settings;

    public CaptureFrameViewModel(ISettingsService settings, OverlayState overlay)
    {
        _settings = settings;
        Overlay = overlay;
    }

    public OverlayState Overlay { get; }

    public CaptureRegion Region => _settings.Current.Capture;

    public void UpdateRegion(CaptureRegion region) => _settings.Update(s => s with { Capture = region });
}
