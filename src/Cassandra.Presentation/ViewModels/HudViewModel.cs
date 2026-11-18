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
    public const int MaxLines = 6;

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
        Models.FirstRunPrompt += () => IsSettingsOpen = true;
    }

    public OverlayState Overlay { get; }
    public SettingsViewModel Settings { get; }
    public ModelsViewModel Models { get; }
    public UpdateViewModel Update { get; }
    public SourceEditorViewModel SourceEditor { get; }
    public ReplyViewModel Reply { get; }
    public ObservableCollection<LineViewModel> Lines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalTab), nameof(IsReplyTab), nameof(LanguageLabel))]
    private EditorTab _activeTab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    public bool IsRunning => !IsPaused;
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsOriginalTab => ActiveTab == EditorTab.Original;
    public bool IsReplyTab => ActiveTab == EditorTab.Reply;

    public string LanguageLabel =>
        (IsReplyTab ? _settings.Current.Languages.Reversed() : _settings.Current.Languages).ToString().ToUpperInvariant();

    /// <summary>The view should move keyboard focus into the reply input.</summary>
    public event Action? ReplyFocusRequested;

    public event Action? CloseRequested;

    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        if (IsPaused)
        {
            await ResumeAsync();
        }
        else
        {
            ActiveTab = EditorTab.Original;
            await PauseAsync();
        }
    }

    [RelayCommand]
    private async Task OpenReplyAsync()
    {
        if (IsPaused && IsReplyTab)
        {
            await ResumeAsync();
            return;
        }

        ActiveTab = EditorTab.Reply;
        if (!IsPaused)
        {
            await PauseAsync();
        }

        Reply.RefreshCaptions();
        ReplyFocusRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

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

    private async Task PauseAsync()
    {
        _reply.Begin(); // remember the game window before the HUD becomes focusable
        await _loop.PauseAsync();
        SourceEditor.Load(Lines.Select(l => l.Original));
        IsPaused = true;
    }

    private async Task ResumeAsync()
    {
        IsPaused = false;
        ActiveTab = EditorTab.Original;
        _reply.ReturnToGame();
        await Task.Yield();
        _loop.Resume();
    }

    private void OnLinesTranslated(IReadOnlyList<TranslatedLine> lines) => _ui.Post(() =>
    {
        Status = null;
        foreach (var line in lines)
        {
            Lines.Add(new LineViewModel(line));
        }

        TrimLines();
    });

    private void OnFailed(Exception ex) => _ui.Post(() =>
    {
        // a missing model is explained by the dedicated prompt in the translation area
        if (ex is not ModelNotInstalledException)
        {
            Status = ex.Message;
        }
    });

    private void ShowManualLines(IReadOnlyList<TranslatedLine> lines)
    {
        Lines.Clear();
        foreach (var line in lines)
        {
            Lines.Add(new LineViewModel(line));
        }

        TrimLines();
    }

    private void TrimLines()
    {
        while (Lines.Count > MaxLines)
        {
            Lines.RemoveAt(0);
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
