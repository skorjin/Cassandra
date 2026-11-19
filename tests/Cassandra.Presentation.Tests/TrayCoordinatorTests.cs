using Cassandra.Domain.Abstractions;
using Cassandra.Presentation.Services;

namespace Cassandra.Presentation.Tests;

public class TrayCoordinatorTests
{
    private sealed class FakeTray : ITrayIcon
    {
        public Func<IReadOnlyList<TrayMenuItem>>? Menu;
        public Action? DoubleClick;
        public bool Disposed;

        public void Show(string tooltip, Func<IReadOnlyList<TrayMenuItem>> menu, Action onDoubleClick)
        {
            Menu = menu;
            DoubleClick = onDoubleClick;
        }

        public void Dispose() => Disposed = true;
    }

    private static void Invoke(FakeTray tray, string text) =>
        tray.Menu!().Single(i => i.Text == text).Invoke!();

    [Fact]
    public async Task Menu_follows_the_state_and_its_items_drive_the_overlay()
    {
        var h = new Harness();
        var tray = new FakeTray();
        using var coordinator = new TrayCoordinator(tray, h.Hud, h.Hud.Overlay);
        coordinator.Start();

        Assert.Contains(tray.Menu!(), i => i.Text == "Скрыть окно");
        Invoke(tray, "Скрыть окно");
        Assert.False(h.Hud.Overlay.IsVisible);
        Assert.Contains(tray.Menu!(), i => i.Text == "Показать окно");

        tray.DoubleClick!(); // double click brings the overlay back
        Assert.True(h.Hud.Overlay.IsVisible);

        Invoke(tray, "Пауза");
        await Task.Delay(50);
        Assert.True(h.Hud.IsPaused);
        Assert.Contains(tray.Menu!(), i => i.Text == "Продолжить перевод");

        Invoke(tray, "Двигать рамку захвата");
        Assert.True(h.Hud.Overlay.IsFrameEditMode);
        Assert.True(tray.Menu!().Single(i => i.Text == "Двигать рамку захвата").IsChecked);
    }

    [Fact]
    public void Settings_item_shows_the_overlay_and_opens_the_drawer()
    {
        var h = new Harness();
        h.Hud.Overlay.IsVisible = false;
        var tray = new FakeTray();
        using var coordinator = new TrayCoordinator(tray, h.Hud, h.Hud.Overlay);
        coordinator.Start();

        Invoke(tray, "Настройки");

        Assert.True(h.Hud.Overlay.IsVisible);
        Assert.True(h.Hud.IsSettingsOpen);
    }

    [Fact]
    public void Reset_item_raises_the_layout_reset_and_shows_the_overlay()
    {
        var h = new Harness();
        h.Hud.Overlay.IsVisible = false;
        var raised = 0;
        h.Hud.Overlay.LayoutResetRequested += () => raised++;
        var tray = new FakeTray();
        using var coordinator = new TrayCoordinator(tray, h.Hud, h.Hud.Overlay);
        coordinator.Start();

        Invoke(tray, "Сбросить расположение окон");

        Assert.Equal(1, raised);
        Assert.True(h.Hud.Overlay.IsVisible);
    }

    [Fact]
    public void Disposing_the_coordinator_removes_the_icon()
    {
        var h = new Harness();
        var tray = new FakeTray();
        new TrayCoordinator(tray, h.Hud, h.Hud.Overlay).Dispose();
        Assert.True(tray.Disposed);
    }
}
