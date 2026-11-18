using Cassandra.Application.Settings;
using Cassandra.Application.Translation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

/// <summary>Player writes in their language, gets a translation, copies it and goes back to the game to paste it.</summary>
public sealed partial class ReplyViewModel : ObservableObject
{
    private readonly IManualTranslationService _translation;
    private readonly IReplyService _reply;
    private readonly ISettingsService _settings;
    private string _translatedFrom = string.Empty;

    public ReplyViewModel(IManualTranslationService translation, IReplyService reply, ISettingsService settings)
    {
        _translation = translation;
        _reply = reply;
        _settings = settings;
    }

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _output = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isCopied;

    [ObservableProperty]
    private string? _error;

    /// <summary>"Английский (для чата)" etc., derived from the current language pair.</summary>
    public string OutputCaption => _settings.Current.Languages.Source switch
    {
        "en" => "Английский (для чата)",
        "ru" => "Русский (для чата)",
        _ => "Перевод",
    };

    public string InputCaption => _settings.Current.Languages.Target switch
    {
        "ru" => "Ваш текст по-русски",
        "en" => "Ваш текст по-английски",
        _ => "Ваш текст",
    };

    public void RefreshCaptions()
    {
        OnPropertyChanged(nameof(OutputCaption));
        OnPropertyChanged(nameof(InputCaption));
    }

    partial void OnInputChanged(string value) => IsCopied = false;

    [RelayCommand]
    private async Task TranslateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var source = Input;
            Output = await _translation.TranslateReplyAsync(source, CancellationToken.None);
            _translatedFrom = source;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Translates first when the text changed since the last translation, then copies and returns focus to the game.</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            return;
        }

        if (Input != _translatedFrom || string.IsNullOrEmpty(Output))
        {
            await TranslateAsync();
        }

        if (string.IsNullOrEmpty(Output))
        {
            return;
        }

        _reply.CopyAndReturnToGame(Output);
        IsCopied = true;
    }
}
