using Cassandra.Domain.Abstractions;
using Cassandra.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cassandra.Presentation.ViewModels;

/// <summary>Application updates: check, show what is new, download and restart.</summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly IAppUpdater _updater;
    private readonly IUiDispatcher _ui;

    public UpdateViewModel(IAppUpdater updater, IUiDispatcher ui)
    {
        _updater = updater;
        _ui = ui;
        VersionText = "Версия " + updater.CurrentVersion;
        Status = updater.IsInstalled ? string.Empty : "Обновления работают в установленной версии приложения.";
    }

    public string VersionText { get; }

    [ObservableProperty] private string _status;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheck))]
    private bool _hasUpdate;

    [ObservableProperty] private string _installLabel = string.Empty;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _showProgress;

    public bool CanCheck => !HasUpdate;

    /// <summary>Quiet check at startup: failures stay silent, an available update lights up the gear icon.</summary>
    public async Task CheckSilentlyAsync()
    {
        try
        {
            await CheckAsync();
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (!_updater.IsInstalled || IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "Проверяю обновления…";
        try
        {
            var update = await _updater.CheckAsync(CancellationToken.None);
            if (update is null)
            {
                Status = "У вас последняя версия.";
                HasUpdate = false;
            }
            else
            {
                HasUpdate = true;
                InstallLabel = $"Установить {update.Version} ({ByteFormat.Format(update.SizeBytes)})";
                Status = $"Доступна версия {update.Version}. Приложение перезапустится.";
            }
        }
        catch (Exception)
        {
            Status = "Не удалось проверить обновления. Проверьте подключение к интернету.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!HasUpdate || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ShowProgress = true;
        Status = "Скачиваю обновление…";
        try
        {
            var progress = new Progress<int>(p => _ui.Post(() => ProgressPercent = p));
            await _updater.DownloadAndRestartAsync(progress, CancellationToken.None); // restarts the app on success
        }
        catch (Exception)
        {
            Status = "Не удалось скачать обновление. Попробуйте ещё раз позже.";
            ShowProgress = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
