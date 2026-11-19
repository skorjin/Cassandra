using Cassandra.Domain.Models;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Tests;

public class HudViewModelTests
{
    [Fact]
    public void Each_frame_replaces_what_was_shown()
    {
        var h = new Harness();
        var a = new TranslatedLine("o a", "text a", "Nick");
        var b = new TranslatedLine("o b", "text b");
        var c = new TranslatedLine("o c", "text c");
        var d = new TranslatedLine("o d", "text d");

        h.Loop.RaiseLines(a, b, c);
        Assert.Equal(["text a", "text b", "text c"], h.Hud.Lines.Select(l => l.Text));
        Assert.Equal("Nick:", h.Hud.Lines[0].Speaker);

        h.Loop.RaiseLines(b, c, d); // chat scrolled by one line
        Assert.Equal(["text b", "text c", "text d"], h.Hud.Lines.Select(l => l.Text));

        h.Loop.RaiseLines(d); // the chat now shows less
        Assert.Equal(["text d"], h.Hud.Lines.Select(l => l.Text));

        h.Loop.RaiseLines(a, b); // unrelated content
        Assert.Equal(["text a", "text b"], h.Hud.Lines.Select(l => l.Text));
    }

    [Fact]
    public void A_scrolled_chat_keeps_the_lines_that_stay_and_only_touches_the_edges()
    {
        var h = new Harness();
        var a = new TranslatedLine("o a", "text a");
        var b = new TranslatedLine("o b", "text b");
        var c = new TranslatedLine("o c", "text c");
        h.Loop.RaiseLines(a, b);
        var keptB = h.Hud.Lines[1];

        h.Loop.RaiseLines(b, c);

        Assert.Same(keptB, h.Hud.Lines[0]); // the same view model: selection and scroll position survive
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
        Assert.Equal(1, h.Loop.Resumes);
        Assert.Equal("EN→RU", h.Hud.LanguageLabel); // live translation shows the chat direction even if Reply was the last tab
    }

    [Fact]
    public async Task Pencil_toggles_the_panel_and_remembers_the_selected_tab()
    {
        var h = new Harness();
        await h.Hud.OpenReplyCommand.ExecuteAsync(null);
        Assert.True(h.Hud.IsReplyTab);

        h.Hud.SelectOriginalTabCommand.Execute(null);
        await h.Hud.OpenReplyCommand.ExecuteAsync(null); // closes the panel, does not jump to Reply
        Assert.False(h.Hud.IsPaused);

        await h.Hud.OpenReplyCommand.ExecuteAsync(null); // reopens on the remembered tab
        Assert.True(h.Hud.IsPaused);
        Assert.True(h.Hud.IsOriginalTab);
    }

    [Fact]
    public void Capture_frame_is_locked_on_a_fresh_start_and_unlocked_by_the_button()
    {
        var h = new Harness();
        Assert.False(h.Hud.Overlay.CanEditFrame);

        h.Hud.ToggleFrameEditCommand.Execute(null);
        Assert.True(h.Hud.Overlay.CanEditFrame);

        h.Hud.ToggleFrameEditCommand.Execute(null);
        Assert.False(h.Hud.Overlay.CanEditFrame);
    }

    [Fact]
    public async Task Pausing_does_not_unlock_the_capture_frame()
    {
        var h = new Harness();
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);
        Assert.False(h.Hud.Overlay.CanEditFrame);

        h.Hud.ToggleFrameEditCommand.Execute(null);
        await h.Hud.TogglePauseCommand.ExecuteAsync(null); // resuming leaves the frame setting alone
        Assert.True(h.Hud.Overlay.CanEditFrame);
    }

    [Fact]
    public async Task Pause_does_not_open_the_editor_panel()
    {
        var h = new Harness();
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        Assert.True(h.Hud.IsPaused);
        Assert.False(h.Hud.IsEditorOpen);
    }

    [Fact]
    public async Task Pencil_after_a_manual_pause_opens_and_closes_the_panel_but_stays_paused()
    {
        var h = new Harness();
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        await h.Hud.OpenReplyCommand.ExecuteAsync(null);
        Assert.True(h.Hud.IsEditorOpen);

        await h.Hud.OpenReplyCommand.ExecuteAsync(null);
        Assert.False(h.Hud.IsEditorOpen);
        Assert.True(h.Hud.IsPaused);
    }

    [Fact]
    public async Task Pause_button_closes_the_open_panel_and_resumes()
    {
        var h = new Harness();
        await h.Hud.OpenReplyCommand.ExecuteAsync(null);
        await h.Hud.TogglePauseCommand.ExecuteAsync(null);

        Assert.False(h.Hud.IsEditorOpen);
        Assert.False(h.Hud.IsPaused);
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

