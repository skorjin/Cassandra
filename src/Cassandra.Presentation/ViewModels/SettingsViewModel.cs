using Cassandra.Application.Settings;
using Cassandra.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

public sealed partial class TintOption : ObservableObject
{
    public TintOption(string hex, string name, bool selected)
    {
        Hex = hex;
        Name = name;
        _isSelected = selected;
    }

    public string Hex { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Pages of the settings drawer. New settings get a page here instead of lengthening an existing one.</summary>
public enum SettingsSection
{
    Appearance,
    Languages,
    Models,
    App,
}

/// <summary>Appearance, language pair and drawer navigation. Every change goes straight to the settings service.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const double MinFontSize = 10;
    public const double MaxFontSize = 30;

    private readonly ISettingsService _settings;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var current = settings.Current;
        _opacity = current.Opacity;
        _fontSize = Math.Clamp(current.FontSize, MinFontSize, MaxFontSize);
        _isEnRu = current.Languages == LanguagePair.EnRu;

        TextColors =
        [
            new("#EEF1F6", "Белый", false),
            new("#FFE08A", "Жёлтый", false),
            new("#9BE7A6", "Зелёный", false),
            new("#8FD3FF", "Голубой", false),
            new("#FFB38A", "Оранжевый", false),
            new("#D6B8FF", "Сиреневый", false),
        ];
        _textColor = current.TextColor;
        foreach (var color in TextColors)
        {
            color.IsSelected = string.Equals(color.Hex, current.TextColor, StringComparison.OrdinalIgnoreCase);
        }

        Tints =
        [
            new("#12161E", "Графит", false),
            new("#1C2A44", "Синий", false),
            new("#2A1C3D", "Фиолетовый", false),
            new("#173326", "Зелёный", false),
            new("#3A1D1D", "Красный", false),
            new("#000000", "Чёрный", false),
        ];
        _tintColor = current.TintColor;
        foreach (var tint in Tints)
        {
            tint.IsSelected = string.Equals(tint.Hex, current.TintColor, StringComparison.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyList<TintOption> Tints { get; }
    public IReadOnlyList<TintOption> TextColors { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppearance), nameof(IsLanguages), nameof(IsModels), nameof(IsApp))]
    private SettingsSection _section = SettingsSection.Appearance;

    public bool IsAppearance => Section == SettingsSection.Appearance;
    public bool IsLanguages => Section == SettingsSection.Languages;
    public bool IsModels => Section == SettingsSection.Models;
    public bool IsApp => Section == SettingsSection.App;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private string _textColor;

    [ObservableProperty]
    private double _opacity;

    [ObservableProperty]
    private string _tintColor;

    [ObservableProperty]
    private bool _isEnRu;

    public bool IsRuEn => !IsEnRu;

    public LanguagePair Languages => IsEnRu ? LanguagePair.EnRu : LanguagePair.EnRu.Reversed();

    partial void OnOpacityChanged(double value) =>
        _settings.Update(s => s with { Opacity = Math.Round(Math.Clamp(value, 0.3, 1.0), 2) });

    partial void OnFontSizeChanged(double value) =>
        _settings.Update(s => s with { FontSize = Math.Round(Math.Clamp(value, MinFontSize, MaxFontSize)) });

    partial void OnTextColorChanged(string value) => _settings.Update(s => s with { TextColor = value });

    [RelayCommand]
    private void SelectTextColor(TintOption option)
    {
        foreach (var color in TextColors)
        {
            color.IsSelected = color == option;
        }

        TextColor = option.Hex;
    }

    [RelayCommand]
    private void ShowSection(SettingsSection section) => Section = section;

    partial void OnTintColorChanged(string value) => _settings.Update(s => s with { TintColor = value });

    partial void OnIsEnRuChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRuEn));
        _settings.Update(s => s with { Languages = Languages });
        LanguagesChanged?.Invoke();
    }

    public event Action? LanguagesChanged;

    [RelayCommand]
    private void SelectTint(TintOption option)
    {
        foreach (var tint in Tints)
        {
            tint.IsSelected = tint == option;
        }

        TintColor = option.Hex;
    }

    [RelayCommand]
    private void SelectEnRu() => IsEnRu = true;

    [RelayCommand]
    private void SelectRuEn() => IsEnRu = false;
}
