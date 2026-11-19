namespace Cassandra.Domain.Models;

public sealed record AppSettings
{
    public WindowBounds Hud { get; init; } = new(100, 100, 340, 180);

    /// <summary>Minimum height of the translated-text area, set by resizing the HUD; 0 means fit the content.</summary>
    public double HudTextHeight { get; init; }

    public CaptureRegion Capture { get; init; } = new(100, 600, 380, 120);
    public double Opacity { get; init; } = 0.82;
    public string TintColor { get; init; } = "#12161E";
    public double FontSize { get; init; } = 14;
    public string TextColor { get; init; } = "#EEF1F6";
    public LanguagePair Languages { get; init; } = LanguagePair.EnRu;
    public int CaptureIntervalMs { get; init; } = 500;

    /// <summary>Id of the translation model variant in use; null until the player installs one.</summary>
    public string? ActiveModelId { get; init; }
}
