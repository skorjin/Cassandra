using System.Net.Http;
using Cassandra.Application.Models;
using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Application.Tests;

public class ModelManagerTests
{
    private static readonly ModelVariant Fast = new("fast", "Fast", "int8", "d", 1, [new ModelFile("a", "https://x/a", 1000, new string('0', 64))]);
    private static readonly ModelVariant Accurate = new("accurate", "Accurate", "fp32", "d", 1, [new ModelFile("a", "https://x/b", 4000, new string('1', 64))]);

    private sealed class FakeCatalog : IModelCatalog
    {
        public Task<IReadOnlyList<ModelVariant>> LoadAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ModelVariant>>([Fast, Accurate]);
    }

    private sealed class FakeStore : IModelStore
    {
        public readonly Dictionary<string, ModelState> States = new();
        public long Partial;

        public IReadOnlyList<ModelStatus> GetStatuses(IReadOnlyList<ModelVariant> variants) =>
            variants.Select(v =>
            {
                var state = States.GetValueOrDefault(v.Id, ModelState.NotInstalled);
                return new ModelStatus(v, state, state == ModelState.Partial ? Partial : 0);
            }).ToList();

        public string? GetInstalledDirectory(string variantId) => States.GetValueOrDefault(variantId) == ModelState.Installed ? "dir" : null;

        public Task DeleteAsync(string variantId)
        {
            States.Remove(variantId);
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedDownloader(FakeStore store) : IModelDownloader
    {
        public int Calls;
        public Func<int, ModelVariant, IProgress<DownloadProgress>?, CancellationToken, Task> Behavior = (_, _, _, _) => Task.CompletedTask;

        public async Task DownloadAsync(ModelVariant variant, IProgress<DownloadProgress>? progress, CancellationToken ct)
        {
            var call = ++Calls;
            store.States[variant.Id] = ModelState.Partial;
            await Behavior(call, variant, progress, ct);
            store.States[variant.Id] = ModelState.Installed;
        }
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings Load() => new();
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (ModelManager Manager, FakeStore Store, ScriptedDownloader Downloader, SettingsService Settings) Create()
    {
        var store = new FakeStore();
        var downloader = new ScriptedDownloader(store);
        var settings = new SettingsService(new MemorySettingsStore());
        var manager = new ModelManager(new FakeCatalog(), store, downloader, settings, backoff: _ => TimeSpan.Zero);
        return (manager, store, downloader, settings);
    }

    private static async Task<DownloadState> WaitForAsync(ModelManager manager, Func<DownloadState, bool> done)
    {
        for (var i = 0; i < 300; i++)
        {
            if (done(manager.State))
            {
                return manager.State;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"State stuck at {manager.State}");
    }

    [Fact]
    public async Task First_installed_model_becomes_active_automatically()
    {
        var (manager, _, _, settings) = Create();
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && manager.Statuses[0].State == ModelState.Installed);

        Assert.Equal("fast", settings.Current.ActiveModelId);
    }

    [Fact]
    public async Task Downloading_a_second_model_does_not_switch_to_it()
    {
        var (manager, store, _, settings) = Create();
        store.States["fast"] = ModelState.Installed;
        settings.Update(s => s with { ActiveModelId = "fast" });
        await manager.InitializeAsync();

        manager.StartDownload("accurate");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && manager.Statuses[1].State == ModelState.Installed);

        Assert.Equal("fast", settings.Current.ActiveModelId);
    }

    [Fact]
    public async Task Network_errors_are_retried_automatically_until_the_download_succeeds()
    {
        var (manager, _, downloader, settings) = Create();
        downloader.Behavior = (call, _, _, _) => call < 4 ? throw new HttpRequestException("network down") : Task.CompletedTask;
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && downloader.Calls == 4);

        Assert.Equal(4, downloader.Calls);
        Assert.Equal("fast", settings.Current.ActiveModelId);
    }

    [Fact]
    public async Task Gives_up_after_many_failures_and_can_be_continued_by_the_player()
    {
        var (manager, _, downloader, _) = Create();
        downloader.Behavior = (_, _, _, _) => throw new IOException("reset");
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        var failed = await WaitForAsync(manager, s => s.Phase == DownloadPhase.Failed);

        Assert.Equal(9, downloader.Calls); // first try + 8 retries
        Assert.Contains("Продолжить", failed.Message);
        Assert.Equal(ModelState.Partial, manager.Statuses[0].State);

        downloader.Behavior = (_, _, _, _) => Task.CompletedTask; // network is back
        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && manager.Statuses[0].State == ModelState.Installed);
    }

    [Fact]
    public async Task Progress_resets_the_retry_budget()
    {
        var (manager, _, downloader, _) = Create();
        long bytes = 0;
        downloader.Behavior = (call, _, progress, _) =>
        {
            if (call <= 12)
            {
                bytes += 100; // every attempt gets further before failing
                progress!.Report(new DownloadProgress(bytes, 1000, "a"));
                return Task.FromException(new HttpRequestException("drop"));
            }

            return Task.CompletedTask;
        };
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && downloader.Calls == 13);

        Assert.Equal(ModelState.Installed, manager.Statuses[0].State);
    }

    [Fact]
    public async Task Cancel_pauses_and_keeps_partial_data_then_resumes()
    {
        var (manager, store, downloader, _) = Create();
        store.Partial = 500;
        downloader.Behavior = async (call, _, _, ct) =>
        {
            if (call == 1)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
        };
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Downloading);
        manager.Cancel();
        var paused = await WaitForAsync(manager, s => s.Phase == DownloadPhase.Paused);

        Assert.Contains("приостановлена", paused.Message);
        Assert.Equal(ModelState.Partial, manager.Statuses[0].State);
        Assert.Equal(500, manager.Statuses[0].DownloadedBytes);

        manager.StartDownload("fast");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && manager.Statuses[0].State == ModelState.Installed);
        Assert.Equal(2, downloader.Calls);
    }

    [Fact]
    public async Task Cancelling_during_the_wait_before_a_retry_pauses()
    {
        var (manager, store, downloader, _) = Create();
        var slow = new ModelManager(new FakeCatalog(), store, downloader, new SettingsService(new MemorySettingsStore()), _ => TimeSpan.FromSeconds(30));
        downloader.Behavior = (_, _, _, _) => throw new HttpRequestException("offline");
        await slow.InitializeAsync();

        slow.StartDownload("fast");
        var waiting = await WaitForAsync(slow, s => s.Phase == DownloadPhase.WaitingToRetry);
        slow.Cancel();
        await WaitForAsync(slow, s => s.Phase == DownloadPhase.Paused);

        Assert.Contains("Повтор через", waiting.Message);
        Assert.Equal(1, downloader.Calls);
    }

    [Fact]
    public async Task Disk_full_is_not_retried()
    {
        var (manager, _, downloader, _) = Create();
        downloader.Behavior = (_, _, _, _) => throw new InsufficientDiskSpaceException(100L * 1024 * 1024);
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        var failed = await WaitForAsync(manager, s => s.Phase == DownloadPhase.Failed);

        Assert.Equal(1, downloader.Calls);
        Assert.Contains("места", failed.Message);
    }

    [Fact]
    public async Task Only_one_download_runs_at_a_time()
    {
        var (manager, _, downloader, _) = Create();
        downloader.Behavior = (_, _, _, ct) => Task.Delay(300, ct);
        await manager.InitializeAsync();

        manager.StartDownload("fast");
        manager.StartDownload("accurate");
        await WaitForAsync(manager, s => s.Phase == DownloadPhase.Idle && manager.Statuses[0].State == ModelState.Installed);

        Assert.Equal(1, downloader.Calls);
        Assert.Equal(ModelState.NotInstalled, manager.Statuses[1].State);
    }

    [Fact]
    public async Task Deleting_the_active_model_clears_the_choice()
    {
        var (manager, store, _, settings) = Create();
        store.States["fast"] = ModelState.Installed;
        settings.Update(s => s with { ActiveModelId = "fast" });
        await manager.InitializeAsync();

        await manager.DeleteAsync("fast");

        Assert.Null(settings.Current.ActiveModelId);
        Assert.Equal(ModelState.NotInstalled, manager.Statuses[0].State);
    }

    [Fact]
    public async Task A_stale_active_model_is_dropped_on_startup()
    {
        var (manager, _, _, settings) = Create();
        settings.Update(s => s with { ActiveModelId = "fast" }); // files were removed by hand

        await manager.InitializeAsync();

        Assert.Null(settings.Current.ActiveModelId);
    }

    [Fact]
    public async Task Activating_a_model_that_is_not_installed_is_refused()
    {
        var (manager, _, _, _) = Create();
        await manager.InitializeAsync();

        Assert.Throws<InvalidOperationException>(() => manager.Activate("fast"));
    }

    [Fact]
    public async Task Activating_an_installed_model_switches_to_it()
    {
        var (manager, store, _, settings) = Create();
        store.States["fast"] = ModelState.Installed;
        store.States["accurate"] = ModelState.Installed;
        await manager.InitializeAsync();

        manager.Activate("accurate");

        Assert.Equal("accurate", settings.Current.ActiveModelId);
    }
}
