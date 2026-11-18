using Cassandra.Application.Models;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Tests;

public class ModelsAndUpdateTests
{
    private static ModelVariant Variant(string id, long size) =>
        new(id, id.ToUpperInvariant(), "int8", "desc", 1, [new ModelFile("a", "https://x/a", size, new string('0', 64))]);

    private static Harness Prepare(ModelState fast = ModelState.NotInstalled, ModelState accurate = ModelState.NotInstalled, string? active = null)
    {
        var h = new Harness();
        h.Models.Statuses =
        [
            new ModelStatus(Variant("fast", 219L << 20), fast, fast == ModelState.Partial ? 100L << 20 : 0),
            new ModelStatus(Variant("accurate", 839L << 20), accurate, 0),
        ];
        h.Models.ActiveModelId = active;
        h.Models.Raise();
        return h;
    }

    private static ModelOptionViewModel Option(Harness h, string id) => h.ModelsVm.Options.Single(o => o.Id == id);

    [Fact]
    public async Task Without_a_model_the_hud_asks_for_one_and_opens_the_settings_on_first_run()
    {
        var h = Prepare();

        Assert.True(h.ModelsVm.NeedsModel);
        await h.ModelsVm.InitializeAsync();
        Assert.True(h.Hud.IsSettingsOpen);
    }

    [Fact]
    public async Task With_an_active_model_nothing_pops_up()
    {
        var h = Prepare(fast: ModelState.Installed, active: "fast");

        await h.ModelsVm.InitializeAsync();

        Assert.False(h.ModelsVm.NeedsModel);
        Assert.False(h.Hud.IsSettingsOpen);
    }

    [Fact]
    public void Download_asks_for_confirmation_with_the_size_before_starting()
    {
        var h = Prepare();
        var accurate = Option(h, "accurate");

        accurate.RequestDownloadCommand.Execute(null);

        Assert.True(accurate.IsConfirming);
        Assert.Contains("839 МБ", accurate.ConfirmText);
        Assert.Empty(h.Models.Started);

        accurate.ConfirmCommand.Execute(null);

        Assert.Equal(["accurate"], h.Models.Started);
        Assert.False(accurate.IsConfirming);
    }

    [Fact]
    public void Cancelling_the_confirmation_downloads_nothing()
    {
        var h = Prepare();
        var fast = Option(h, "fast");
        fast.RequestDownloadCommand.Execute(null);

        fast.CancelPendingCommand.Execute(null);

        Assert.False(fast.IsConfirming);
        Assert.Empty(h.Models.Started);
        Assert.True(fast.ShowDownload);
    }

    [Fact]
    public void Continuing_a_partial_download_does_not_ask_again()
    {
        var h = Prepare(fast: ModelState.Partial);
        var fast = Option(h, "fast");

        Assert.Equal("Продолжить", fast.DownloadLabel);
        Assert.Contains("100 МБ из 219 МБ", fast.StateText);

        fast.RequestDownloadCommand.Execute(null);

        Assert.Equal(["fast"], h.Models.Started);
    }

    [Fact]
    public void Progress_and_pause_are_shown_only_on_the_downloading_model()
    {
        var h = Prepare();
        h.Models.State = new DownloadState(DownloadPhase.Downloading, "fast", 110L << 20, 219L << 20);
        h.Models.Raise();

        var fast = Option(h, "fast");
        var accurate = Option(h, "accurate");

        Assert.True(fast.IsDownloading);
        Assert.True(fast.ShowPause);
        Assert.Equal(50.2, fast.ProgressPercent, 1);
        Assert.Contains("110 МБ из 219 МБ", fast.ProgressText);
        Assert.False(accurate.IsDownloading);
        Assert.False(accurate.ShowDownload); // one download at a time
    }

    [Fact]
    public void Network_trouble_is_explained_on_the_model_row()
    {
        var h = Prepare();
        h.Models.State = new DownloadState(DownloadPhase.WaitingToRetry, "fast", 10, 100, 2, "Нет связи (таймаут). Повтор через 5 с…", TimeSpan.FromSeconds(5));
        h.Models.Raise();

        var fast = Option(h, "fast");

        Assert.True(fast.HasMessage);
        Assert.Contains("Нет связи", fast.Message);
        Assert.True(fast.IsDownloading);
        Assert.True(fast.ShowPause);
    }

    [Fact]
    public void After_giving_up_the_player_can_continue()
    {
        var h = Prepare(fast: ModelState.Partial);
        h.Models.State = new DownloadState(DownloadPhase.Failed, "fast", 100L << 20, 219L << 20, 9, "Связь прервана.");
        h.Models.Raise();

        var fast = Option(h, "fast");

        Assert.True(fast.IsError);
        Assert.False(fast.IsDownloading);
        Assert.True(fast.ShowDownload);
        Assert.Equal("Продолжить", fast.DownloadLabel);
    }

    [Fact]
    public void Installed_model_can_be_activated_and_the_hud_stops_asking()
    {
        var h = Prepare(fast: ModelState.Installed);
        var fast = Option(h, "fast");
        Assert.True(h.ModelsVm.NeedsModel);
        Assert.True(fast.ShowActivate);

        fast.ActivateCommand.Execute(null);

        Assert.False(h.ModelsVm.NeedsModel);
        Assert.True(fast.IsActive);
        Assert.False(fast.ShowActivate);
        Assert.Contains("используется", fast.StateText);
    }

    [Fact]
    public async Task Deleting_needs_confirmation()
    {
        var h = Prepare(fast: ModelState.Installed, active: "fast");
        var fast = Option(h, "fast");

        fast.RequestDeleteCommand.Execute(null);
        Assert.True(fast.IsConfirming);
        Assert.Empty(h.Models.Deleted);

        await fast.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(["fast"], h.Models.Deleted);
    }

    [Fact]
    public void An_available_model_update_is_offered()
    {
        var h = Prepare(fast: ModelState.UpdateAvailable, active: "fast");
        var fast = Option(h, "fast");

        Assert.Equal("Обновить", fast.DownloadLabel);
        Assert.True(fast.ShowDownload);
        Assert.True(fast.ShowActivate == false);
        Assert.Contains("обновление", fast.StateText);
    }

    [Fact]
    public void Pause_button_pauses_the_manager()
    {
        var h = Prepare();
        h.Models.State = new DownloadState(DownloadPhase.Downloading, "fast", 1, 100);
        h.Models.Raise();

        Option(h, "fast").PauseCommand.Execute(null);

        Assert.Equal(1, h.Models.Cancels);
    }

    [Fact]
    public async Task Update_check_reports_a_new_version_and_shows_the_badge()
    {
        var h = new Harness { Updater = { Available = new AppUpdateInfo("1.1.0", 60L << 20) } };
        var update = new UpdateViewModel(h.Updater, new ImmediateDispatcher());

        await update.CheckCommand.ExecuteAsync(null);

        Assert.True(update.HasUpdate);
        Assert.Contains("1.1.0", update.InstallLabel);
        Assert.Contains("60 МБ", update.InstallLabel);
        Assert.False(update.CanCheck);
    }

    [Fact]
    public async Task Update_check_says_when_the_app_is_current()
    {
        var h = new Harness();
        var update = new UpdateViewModel(h.Updater, new ImmediateDispatcher());

        await update.CheckCommand.ExecuteAsync(null);

        Assert.False(update.HasUpdate);
        Assert.Contains("последняя", update.Status);
    }

    [Fact]
    public async Task Update_check_failure_is_explained_not_thrown()
    {
        var h = new Harness { Updater = { FailCheck = true } };
        var update = new UpdateViewModel(h.Updater, new ImmediateDispatcher());

        await update.CheckCommand.ExecuteAsync(null);

        Assert.Contains("Не удалось", update.Status);
        Assert.False(update.HasUpdate);
    }

    [Fact]
    public async Task Install_downloads_and_restarts()
    {
        var h = new Harness { Updater = { Available = new AppUpdateInfo("2.0.0", 1) } };
        var update = new UpdateViewModel(h.Updater, new ImmediateDispatcher());
        await update.CheckCommand.ExecuteAsync(null);

        await update.InstallCommand.ExecuteAsync(null);

        Assert.True(h.Updater.Restarted);
    }

    [Fact]
    public async Task A_build_folder_cannot_self_update()
    {
        var h = new Harness { Updater = { IsInstalled = false, Available = new AppUpdateInfo("9.9.9", 1) } };
        var update = new UpdateViewModel(h.Updater, new ImmediateDispatcher());

        await update.CheckCommand.ExecuteAsync(null);

        Assert.False(update.HasUpdate);
        Assert.Contains("установленной", update.Status);
    }

    [Fact]
    public void A_missing_model_error_does_not_clutter_the_status_line()
    {
        var h = Prepare();
        h.Loop.RaiseFailure(new ModelNotInstalledException());

        Assert.False(h.Hud.HasStatus);
    }
}
