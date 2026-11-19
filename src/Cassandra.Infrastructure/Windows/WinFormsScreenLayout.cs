using System.Windows.Forms;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Windows;

/// <summary>Monitor layout from WinForms Screen. The process is per-monitor DPI aware, so values are physical pixels.</summary>
public sealed class WinFormsScreenLayout : IScreenLayout
{
    public IReadOnlyList<CaptureRegion> WorkAreas =>
        Screen.AllScreens.Select(s => ToRegion(s.WorkingArea)).ToList();

    public CaptureRegion PrimaryWorkArea => ToRegion((Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea);

    private static CaptureRegion ToRegion(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
