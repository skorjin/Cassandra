using System.Drawing;
using System.Drawing.Imaging;
using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Capture;
using Cassandra.Infrastructure.Ocr;
using Cassandra.Infrastructure.Settings;
using Xunit.Abstractions;

namespace Cassandra.Infrastructure.Tests;

public class OcrAndCaptureTests(ITestOutputHelper output)
{
    private static Frame Render(string[] lines, Color fg, Color bg, float size = 15f)
    {
        using var bmp = new Bitmap(420, 20 + lines.Length * 22, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(bg);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var font = new Font("Consolas", size, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(fg);
            for (var i = 0; i < lines.Length; i++)
            {
                g.DrawString(lines[i], font, brush, 6, 8 + i * 22);
            }
        }

        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bmp.Width * bmp.Height * 4];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        bmp.UnlockBits(data);
        return new Frame(bmp.Width, bmp.Height, pixels);
    }

    [Fact]
    public async Task Reads_light_text_on_dark_background()
    {
        using var ocr = new TesseractOcrEngine();
        var frame = Render(["Kowalski: Enemy sniper on the roof", "Rina: I need a medic at point B"], Color.WhiteSmoke, Color.FromArgb(30, 40, 35));

        var result = await ocr.RecognizeAsync(frame, "en", default);

        output.WriteLine(string.Join(" / ", result.Lines));
        var all = string.Join(" ", result.Lines).ToLowerInvariant();
        Assert.Contains("sniper", all);
        Assert.Contains("roof", all);
        Assert.Contains("medic", all);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public async Task Reads_dark_text_on_light_background_and_russian()
    {
        using var ocr = new TesseractOcrEngine();
        var frame = Render(["Ковальски: Снайпер на крыше"], Color.Black, Color.White, 16f);

        var result = await ocr.RecognizeAsync(frame, "ru", default);

        output.WriteLine(string.Join(" / ", result.Lines));
        Assert.Contains("снайпер", string.Join(" ", result.Lines).ToLowerInvariant());
    }

    [Fact]
    public void Captures_screen_region_in_memory()
    {
        var frame = new GdiScreenCapturer().Capture(new CaptureRegion(0, 0, 120, 60));
        Assert.Equal(120, frame.Width);
        Assert.Equal(60, frame.Height);
        Assert.Equal(120 * 60 * 4, frame.Pixels.Length);
    }

    [Fact]
    public async Task Settings_round_trip_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), "cassandra-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            var saved = new AppSettings
            {
                Hud = new WindowBounds(10, 20, 340, 200),
                Capture = new CaptureRegion(5, 6, 400, 130),
                Opacity = 0.55,
                TintColor = "#1C2A44",
                Languages = new LanguagePair("ru", "en"),
            };
            await store.SaveAsync(saved);
            Assert.Equal(saved, store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Missing_or_corrupt_settings_fall_back_to_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "cassandra-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Assert.Equal(new AppSettings(), new JsonSettingsStore(path).Load());
            File.WriteAllText(path, "{ not json");
            Assert.Equal(new AppSettings(), new JsonSettingsStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
