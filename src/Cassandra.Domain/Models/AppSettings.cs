namespace Cassandra.Domain.Models;

public sealed record AppSettings
{
    public WindowBounds Hud { get; init; } = new(100, 100, 340, 180);
    public CaptureRegion Capture { get; init; } = new(100, 600, 380, 120);
    public double Opacity { get; init; } = 0.82;
    public string TintColor { get; init; } = "#12161E";
    public LanguagePair Languages { get; init; } = LanguagePair.EnRu;
    public int CaptureIntervalMs { get; init; } = 500;

    /// <summary>Id of the translation model variant in use; null until the player installs one.</summary>
    public string? ActiveModelId { get; init; }
}
