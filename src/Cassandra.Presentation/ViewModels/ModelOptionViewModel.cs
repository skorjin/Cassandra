using Cassandra.Application.Models;
using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

public enum PendingAction
{
    None,
    Download,
    Delete,
}

/// <summary>One row of the model list: what it is, what state it is in, and which buttons make sense right now.</summary>
public sealed partial class ModelOptionViewModel : ObservableObject
{
    private readonly ModelsViewModel _owner;
    private ModelState _state;

    public ModelOptionViewModel(ModelsViewModel owner, ModelVariant variant)
    {
        _owner = owner;
        Variant = variant;
        Name = variant.Name;
        Description = variant.Description;
        SizeText = ByteFormat.Format(variant.TotalBytes);
    }

    public ModelVariant Variant { get; }
    public string Id => Variant.Id;
    public string Name { get; }
    public string Description { get; }
    public string SizeText { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private bool _showActivate;
    [ObservableProperty] private bool _showDownload;
    [ObservableProperty] private string _downloadLabel = "Скачать";
    [ObservableProperty] private bool _showPause;
    [ObservableProperty] private bool _showDelete;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMessage))] private string? _message;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsConfirming))] private PendingAction _pending;
    [ObservableProperty] private string _confirmText = string.Empty;

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool IsConfirming => Pending != PendingAction.None;

    public void Update(ModelStatus status, bool isActive, DownloadState download)
    {
        _state = status.State;
        var mine = download.VariantId == Id;
        var downloadingThis = mine && download.IsBusy;
        var otherBusy = download.IsBusy && !mine;

        IsActive = isActive;
        IsDownloading = downloadingThis;

        StateText = status.State switch
        {
            ModelState.Installed => isActive ? "Установлена · используется" : "Установлена",
            ModelState.UpdateAvailable => (isActive ? "Используется · " : string.Empty) + "доступно обновление",
            ModelState.Partial => $"Скачано {ByteFormat.Format(status.DownloadedBytes)} из {SizeText}",
            _ => "Не скачана",
        };

        if (downloadingThis)
        {
            ProgressPercent = Math.Round(download.Fraction * 100, 1);
            ProgressText = $"{ByteFormat.Format(download.DownloadedBytes)} из {ByteFormat.Format(download.TotalBytes)} · {Math.Floor(download.Fraction * 100)}%";
        }
        else
        {
            ProgressText = string.Empty;
        }

        var showMessage = mine && download.Phase is DownloadPhase.WaitingToRetry or DownloadPhase.Paused or DownloadPhase.Failed;
        Message = showMessage ? download.Message : null;
        IsError = mine && download.Phase is DownloadPhase.Failed or DownloadPhase.WaitingToRetry;

        var installed = status.State is ModelState.Installed or ModelState.UpdateAvailable;
        if (otherBusy || downloadingThis)
        {
            Pending = PendingAction.None;
        }

        ShowActivate = installed && !isActive && !downloadingThis;
        ShowDownload = !downloadingThis && !otherBusy && !IsConfirming && status.State != ModelState.Installed;
        DownloadLabel = status.State switch
        {
            ModelState.Partial => "Продолжить",
            ModelState.UpdateAvailable => "Обновить",
            _ => "Скачать " + SizeText,
        };
        ShowPause = downloadingThis;
        ShowDelete = status.State != ModelState.NotInstalled && !downloadingThis && !otherBusy && !IsConfirming;
    }

    partial void OnPendingChanged(PendingAction value) => _owner.RefreshOption(this);

    [RelayCommand]
    private void RequestDownload()
    {
        if (_state == ModelState.Partial)
        {
            _owner.StartDownload(Id); // nothing new to warn about: the player already agreed once
            return;
        }

        ConfirmText = _state == ModelState.UpdateAvailable
            ? $"Будет скачано {SizeText}. Новая версия заменит текущую, пока идёт загрузка она продолжит работать."
            : $"Будет скачано {SizeText} из интернета. Нужно столько же свободного места на диске. Загрузку можно приостановить, она продолжится с того же места.";
        Pending = PendingAction.Download;
    }

    [RelayCommand]
    private void RequestDelete()
    {
        ConfirmText = IsActive
            ? $"Удалить «{Name}»? Перевод перестанет работать, пока вы не выберете другую модель."
            : $"Удалить «{Name}»? Освободится {SizeText}.";
        Pending = PendingAction.Delete;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var action = Pending;
        Pending = PendingAction.None;
        if (action == PendingAction.Download)
        {
            _owner.StartDownload(Id);
        }
        else if (action == PendingAction.Delete)
        {
            await _owner.DeleteAsync(Id);
        }
    }

    [RelayCommand]
    private void CancelPending() => Pending = PendingAction.None;

    [RelayCommand]
    private void Activate() => _owner.Activate(Id);

    [RelayCommand]
    private void Pause() => _owner.Pause();
}
