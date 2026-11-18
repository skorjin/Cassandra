using Cassandra.Application.Pipeline;
using Cassandra.Application.Translation;
using Cassandra.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

/// <summary>Lets the player fix OCR typos in the original text and translate it again.</summary>
public sealed partial class SourceEditorViewModel : ObservableObject
{
    private readonly IManualTranslationService _translation;

    public SourceEditorViewModel(IManualTranslationService translation) => _translation = translation;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    /// <summary>Raised with the freshly translated lines so the HUD can show them.</summary>
    public event Action<IReadOnlyList<TranslatedLine>>? Translated;

    public void Load(IEnumerable<string> originalLines) => Text = string.Join(Environment.NewLine, originalLines);

    [RelayCommand]
    private async Task TranslateAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var result = new List<TranslatedLine>();
            var lines = Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                var parsed = ChatLineParser.Parse(line);
                var translated = await _translation.TranslateOriginalAsync(parsed.Message, CancellationToken.None);
                result.Add(new TranslatedLine(line, translated, parsed.Speaker));
            }

            Translated?.Invoke(result);
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
}
