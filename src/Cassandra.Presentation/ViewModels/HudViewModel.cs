using System.Collections.ObjectModel;
using Cassandra.Application.Pipeline;
using Cassandra.Application.Settings;
using Cassandra.Application.Translation;
using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;
using Cassandra.Presentation.State;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

public enum EditorTab
{
    Original,
    Reply,
}

/// <summary>State and commands of the compact HUD: translated lines, pause, settings drawer, editor tabs.</summary>
public sealed partial class HudViewModel : ObservableObject, IDisposable
{
    private readonly ICaptureLoop _loop;
    private readonly ISettingsService _settings;
    private readonly IReplyService _reply;
    private readonly IUiDispatcher _ui;

    public HudViewModel(
        ICaptureLoop loop,
        ISettingsService settings,
        IReplyService reply,
        IUiDispatcher ui,
        OverlayState overlay,
        SettingsViewModel settingsViewModel,
        ModelsViewModel models,
        UpdateViewModel update,
        SourceEditorViewModel sourceEditor,
        ReplyViewModel replyViewModel)
    {
        _loop = loop;
        _settings = settings;
        _reply = reply;
        _ui = ui;
        Overlay = overlay;
        Settings = settingsViewModel;
        Models = models;
        Update = update;
        SourceEditor = sourceEditor;
        Reply = replyViewModel;

        _loop.LinesTranslated += OnLinesTranslated;
        _loop.Failed += OnFailed;
        SourceEditor.Translated += ShowManualLines;
        Settings.LanguagesChanged += OnLanguagesChanged;
        Models.FirstRunPrompt += OpenModels;
    }

    public OverlayState Overlay { get; }
    public SettingsViewModel Settings { get; }
    public ModelsViewModel Models { get; }
    public UpdateViewModel Update { get; }
    public SourceEditorViewModel SourceEditor { get; }
    public ReplyViewModel Reply { get; }
    public ObservableCollection<LineViewModel> Lines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(LanguageLabel))]
    private bool _isPaused;

    /// <summary>The panel with the Original and Reply tabs is shown. Opened only by the pencil.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageLabel))]
    private bool _isEditorOpen;

    private bool _pausedByEditor;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalTab), nameof(IsReplyTab), nameof(LanguageLabel))]
    private EditorTab _activeTab = EditorTab.Reply; // the pencil is about replies; the last chosen tab is remembered

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    public bool IsRunning => !IsPaused;
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsOriginalTab => ActiveTab == EditorTab.Original;
    public bool IsReplyTab => ActiveTab == EditorTab.Reply;

    public string LanguageLabel =>
        (IsEditorOpen && IsReplyTab ? _settings.Current.Languages.Reversed() : _settings.Current.Languages).ToString().ToUpperInvariant();

    /// <summary>The view should move keyboard focus into the reply input.</summary>
    public event Action? ReplyFocusRequested;

    public event Action? CloseRequested;

    /// <summary>Pause only stops translation. The editor panel is opened by the pencil alone.</summary>
    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        if (IsPaused)
        {
            await ResumeAsync();
        }
        else
        {
            await PauseAsync();
        }
    }

    /// <summary>The pencil only opens and closes the editor panel; the tab that was selected stays selected.</summary>
    [RelayCommand]
    private async Task OpenReplyAsync()
    {
        if (IsEditorOpen)
        {
            IsEditorOpen = false;
            if (_pausedByEditor)
            {
                _pausedByEditor = false;
                await ResumeAsync();
            }

            return;
        }

        // editing needs a still chat; if the player had already paused, they stay paused after closing the panel
        _pausedByEditor = !IsPaused;
        if (!IsPaused)
        {
            await PauseAsync();
        }

        IsEditorOpen = true;
        if (IsReplyTab)
        {
            Reply.RefreshCaptions();
            ReplyFocusRequested?.Invoke();
        }
    }

    /// <summary>Lets the capture frame be moved and resized while translation keeps running.</summary>
    [RelayCommand]
    private void ToggleFrameEdit() => Overlay.IsFrameEditMode = !Overlay.IsFrameEditMode;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void OpenSettings() => OpenModels();

    /// <summary>Opens the drawer on the models page: used when a translation model is missing.</summary>
    private void OpenModels()
    {
        Settings.Section = SettingsSection.Models;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void SelectOriginalTab() => ActiveTab = EditorTab.Original;

    [RelayCommand]
    private void SelectReplyTab()
    {
        ActiveTab = EditorTab.Reply;
        Reply.RefreshCaptions();
        ReplyFocusRequested?.Invoke();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    /// <summary>The player starts selecting translated text: remember the game window so focus can go back to it.</summary>
    public void BeginTextSelection() => _reply.Begin();

    /// <summary>Selection or copying is done: give focus back to the game.</summary>
    public void EndTextSelection() => _reply.ReturnToGame();

    private async Task PauseAsync()
    {
        _reply.Begin(); // remember the game window before the HUD becomes focusable
        await _loop.PauseAsync();
        SourceEditor.Load(Lines.Select(l => l.Original));
        IsPaused = true;
    }

    private async Task ResumeAsync()
    {
        IsEditorOpen = false;
        _pausedByEditor = false;
        IsPaused = false;
        _reply.ReturnToGame();
        await Task.Yield();
        _loop.Resume();
    }

    private void OnLinesTranslated(IReadOnlyList<TranslatedLine> lines) => _ui.Post(() =>
    {
        Status = null;
        ShowSnapshot(lines);
    });
    private void OnFailed(Exception ex) => _ui.Post(() =>
    {
        // a missing model is explained by the dedicated prompt in the translation area
        if (ex is not ModelNotInstalledException)
        {
            Status = ex.Message;
        }
    });

    private void ShowManualLines(IReadOnlyList<TranslatedLine> lines) => ShowSnapshot(lines);

    /// <summary>
    /// Shows exactly what the last frame contained. When the chat only scrolled, lines that stay are kept as they are
    /// (so a selection or scroll position survives), old ones drop off the top and new ones are added at the bottom.
    /// Anything else replaces the whole list.
    /// </summary>
    private void ShowSnapshot(IReadOnlyList<TranslatedLine> lines)
    {
        var current = Lines.Select(l => l.Model).ToList();
        if (current.SequenceEqual(lines))
        {
            return;
        }

        // smallest k such that the last (current - k) shown lines are the first lines of the new snapshot
        for (var k = 0; k < current.Count; k++)
        {
            var overlap = current.Count - k;
            if (overlap <= lines.Count && Enumerable.Range(0, overlap).All(i => current[k + i] == lines[i]))
            {
                for (var i = 0; i < k; i++)
                {
                    Lines.RemoveAt(0);
                }

                foreach (var line in lines.Skip(overlap))
                {
                    Lines.Add(new LineViewModel(line));
                }

                return;
            }
        }

        Lines.Clear();
        foreach (var line in lines)
        {
            Lines.Add(new LineViewModel(line));
        }
    }
    private void OnLanguagesChanged()
    {
        Lines.Clear();
        OnPropertyChanged(nameof(LanguageLabel));
        Reply.RefreshCaptions();
    }

    public void Dispose()
    {
        _loop.LinesTranslated -= OnLinesTranslated;
        _loop.Failed -= OnFailed;
        SourceEditor.Translated -= ShowManualLines;
        Settings.LanguagesChanged -= OnLanguagesChanged;
    }
}
