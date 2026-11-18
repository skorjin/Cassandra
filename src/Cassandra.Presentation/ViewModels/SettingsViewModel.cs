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

/// <summary>Opacity, background tint and language pair. Every change goes straight to the settings service.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var current = settings.Current;
        _opacity = current.Opacity;
        _isEnRu = current.Languages == LanguagePair.EnRu;

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
