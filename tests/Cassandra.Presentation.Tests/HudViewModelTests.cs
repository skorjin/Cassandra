using Cassandra.Domain.Models;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Tests;

public class HudViewModelTests
{
    [Fact]
    public void Translated_lines_appear_and_are_trimmed()
    {
        var h = new Harness();
        for (var i = 0; i < 10; i++)
        {
            h.Loop.RaiseLines(new TranslatedLine($"orig {i}", $"text {i}", "Nick"));
        }

        Assert.Equal(HudViewModel.MaxLines, h.Hud.Lines.Count);
        Assert.Equal("text 9", h.Hud.Lines[^1].Text);
        Assert.Equal("Nick:", h.Hud.Lines[^1].Speaker);
    }

    [Fact]
    public async Task Pause_stops_loop_and_fills_editor_with_original_text()
    {
        var h = new Harness();
        h.Loop.RaiseLines(new TranslatedLine("Nick: hello", "привет", "Nick"), new TranslatedLine("Nick: bye", "пока", "Nick"));

        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        Assert.True(h.Hud.IsPaused);
        Assert.Equal(1, h.Loop.Pauses);
        Assert.Equal(1, h.Reply.Begins);
        Assert.Contains("Nick: hello", h.Hud.SourceEditor.Text);
        Assert.Contains("Nick: bye", h.Hud.SourceEditor.Text);
    }

    [Fact]
    public async Task Resume_restarts_loop_and_returns_focus_to_game()
    {
        var h = new Harness();
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        Assert.False(h.Hud.IsPaused);
        Assert.Equal(1, h.Loop.Resumes);
        Assert.Equal(1, h.Reply.Returns);
    }

    [Fact]
    public async Task Open_reply_pauses_selects_reply_tab_and_requests_focus()
    {
        var h = new Harness();
        var focusRequests = 0;
        h.Hud.ReplyFocusRequested += () => focusRequests++;

        await h.Hud.OpenReplyCommand.ExecuteAsync(null);

        Assert.True(h.Hud.IsPaused);
        Assert.True(h.Hud.IsReplyTab);
        Assert.Equal(1, focusRequests);
        Assert.Equal("RU→EN", h.Hud.LanguageLabel);
    }

    [Fact]
    public async Task Open_reply_twice_goes_back_to_work()
    {
        var h = new Harness();
        await h.Hud.OpenReplyCommand.ExecuteAsync(null);
        await h.Hud.OpenReplyCommand.ExecuteAsync(null);

        Assert.False(h.Hud.IsPaused);
        Assert.True(h.Hud.IsOriginalTab);
        Assert.Equal(1, h.Loop.Resumes);
    }

    [Fact]
    public void Failure_is_shown_as_status_and_cleared_by_next_translation()
    {
        var h = new Harness();
        h.Loop.RaiseFailure(new InvalidOperationException("Model not found"));
        Assert.True(h.Hud.HasStatus);
        Assert.Equal("Model not found", h.Hud.Status);

        h.Loop.RaiseLines(new TranslatedLine("a", "b"));
        Assert.False(h.Hud.HasStatus);
    }

    [Fact]
    public async Task Retranslating_edited_original_replaces_visible_lines()
    {
        var h = new Harness();
        h.Loop.RaiseLines(new TranslatedLine("Nick: helo", "хело", "Nick"));
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        h.Hud.SourceEditor.Text = "Nick: hello";
        await h.Hud.SourceEditor.TranslateCommand.ExecuteAsync(null);

        var line = Assert.Single(h.Hud.Lines);
        Assert.Equal("RU:hello", line.Text);
        Assert.Equal("Nick:", line.Speaker);
    }

    [Fact]
    public void Changing_language_clears_lines_and_updates_label()
    {
        var h = new Harness();
        h.Loop.RaiseLines(new TranslatedLine("a", "b"));

        h.Hud.Settings.SelectRuEnCommand.Execute(null);

        Assert.Empty(h.Hud.Lines);
        Assert.Equal("RU→EN", h.Hud.LanguageLabel);
    }

    [Fact]
    public void Settings_drawer_toggles()
    {
        var h = new Harness();
        h.Hud.ToggleSettingsCommand.Execute(null);
        Assert.True(h.Hud.IsSettingsOpen);
        h.Hud.ToggleSettingsCommand.Execute(null);
        Assert.False(h.Hud.IsSettingsOpen);
    }
}
