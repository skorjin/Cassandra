using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Tests;

public class ReplyAndSettingsTests
{
    [Fact]
    public async Task Copy_translates_changed_text_then_copies_and_returns_to_game()
    {
        var h = new Harness();
        h.ReplyVm.Input = "спасибо";

        await h.ReplyVm.CopyCommand.ExecuteAsync(null);

        Assert.Equal(["EN:спасибо"], h.Reply.Copied);
        Assert.Equal("EN:спасибо", h.ReplyVm.Output);
        Assert.True(h.ReplyVm.IsCopied);
        Assert.Equal(1, h.Reply.Returns);
    }

    [Fact]
    public async Task Copy_does_not_translate_again_when_text_is_unchanged()
    {
        var h = new Harness();
        h.ReplyVm.Input = "спасибо";
        await h.ReplyVm.TranslateCommand.ExecuteAsync(null);

        await h.ReplyVm.CopyCommand.ExecuteAsync(null);

        Assert.Equal(1, h.Manual.ReplyCalls);
        Assert.Single(h.Reply.Copied);
    }

    [Fact]
    public async Task Copy_with_empty_input_does_nothing()
    {
        var h = new Harness();
        await h.ReplyVm.CopyCommand.ExecuteAsync(null);
        Assert.Empty(h.Reply.Copied);
        Assert.Equal(0, h.Manual.ReplyCalls);
    }

    [Fact]
    public async Task Editing_the_text_resets_copied_flag()
    {
        var h = new Harness();
        h.ReplyVm.Input = "да";
        await h.ReplyVm.CopyCommand.ExecuteAsync(null);
        h.ReplyVm.Input = "нет";
        Assert.False(h.ReplyVm.IsCopied);
    }

    [Fact]
    public async Task Font_size_and_color_are_persisted_and_clamped()
    {
        var h = new Harness();
        var vm = h.Hud.Settings;

        vm.FontSize = 20;
        vm.SelectTextColorCommand.Execute(vm.TextColors[2]);
        await Task.Delay(150);
        Assert.Equal(20, h.Store.Saved.FontSize);
        Assert.Equal(vm.TextColors[2].Hex, h.Store.Saved.TextColor);
        Assert.True(vm.TextColors[2].IsSelected);
        Assert.False(vm.TextColors[0].IsSelected);

        vm.FontSize = 99;
        await Task.Delay(150);
        Assert.Equal(SettingsViewModel.MaxFontSize, h.Store.Saved.FontSize);
    }

    [Fact]
    public void Drawer_sections_switch_and_missing_model_opens_the_models_page()
    {
        var h = new Harness();
        var vm = h.Hud.Settings;
        Assert.True(vm.IsAppearance);

        vm.ShowSectionCommand.Execute(SettingsSection.Languages);
        Assert.True(vm.IsLanguages);
        Assert.False(vm.IsAppearance);

        h.Hud.OpenSettingsCommand.Execute(null);
        Assert.True(h.Hud.IsSettingsOpen);
        Assert.True(vm.IsModels);
    }

    [Fact]
    public async Task Settings_changes_are_persisted()
    {
        var h = new Harness();
        var vm = h.Hud.Settings;

        vm.Opacity = 0.55;
        vm.SelectTintCommand.Execute(vm.Tints[1]);
        vm.SelectRuEnCommand.Execute(null);
        await Task.Delay(150);

        Assert.Equal(0.55, h.Store.Saved.Opacity);
        Assert.Equal(vm.Tints[1].Hex, h.Store.Saved.TintColor);
        Assert.Equal(new LanguagePair("ru", "en"), h.Store.Saved.Languages);
        Assert.True(vm.Tints[1].IsSelected);
        Assert.False(vm.Tints[0].IsSelected);
    }

    [Theory]
    [InlineData(HotkeyAction.ToggleLock)]
    [InlineData(HotkeyAction.ToggleVisibility)]
    public void Hotkeys_toggle_overlay_flags(HotkeyAction action)
    {
        var h = new Harness();
        var coordinator = new HotkeyCoordinator(new NoopHotkeys(), h.Hud, h.Overlay, new ImmediateDispatcher());

        coordinator.OnHotkey(action);

        if (action == HotkeyAction.ToggleLock)
        {
            Assert.True(h.Overlay.IsLocked);
        }
        else
        {
            Assert.False(h.Overlay.IsVisible);
        }

        coordinator.OnHotkey(action);
        Assert.False(h.Overlay.IsLocked);
        Assert.True(h.Overlay.IsVisible);
    }

    [Fact]
    public async Task Pause_hotkey_pauses_and_reply_hotkey_shows_overlay()
    {
        var h = new Harness();
        h.Overlay.IsVisible = false;
        var coordinator = new HotkeyCoordinator(new NoopHotkeys(), h.Hud, h.Overlay, new ImmediateDispatcher());

        coordinator.OnHotkey(HotkeyAction.OpenReply);
        await Task.Delay(50);

        Assert.True(h.Overlay.IsVisible);
        Assert.True(h.Hud.IsPaused);
        Assert.True(h.Hud.IsReplyTab);
    }

    private sealed class NoopHotkeys : IHotkeyService
    {
        public event Action<HotkeyAction>? HotkeyPressed { add { } remove { } }
        public void RegisterAll() { }
        public void Dispose() { }
    }
}
