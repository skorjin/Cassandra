using System.Net.Http;
using Cassandra.Application.Models;
using Cassandra.Application.Pipeline;
using Cassandra.Application.Settings;
using Cassandra.Application.Translation;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Tests;

internal sealed class FakeLoop : ICaptureLoop
{
    public PipelineState State { get; private set; } = PipelineState.Running;
    public int Pauses;
    public int Resumes;

    public event Action<PipelineState>? StateChanged;
    public event Action<IReadOnlyList<TranslatedLine>>? LinesTranslated;
    public event Action<Exception>? Failed;

    public void Start() => State = PipelineState.Running;

    public Task PauseAsync()
    {
        Pauses++;
        State = PipelineState.Paused;
        StateChanged?.Invoke(State);
        return Task.CompletedTask;
    }

    public void Resume()
    {
        Resumes++;
        State = PipelineState.Running;
    }

    public void Dispose() { }

    public void RaiseLines(params TranslatedLine[] lines) => LinesTranslated?.Invoke(lines);
    public void RaiseFailure(Exception ex) => Failed?.Invoke(ex);
}

internal sealed class FakeReply : IReplyService
{
    public int Begins;
    public int Returns;
    public readonly List<string> Copied = [];

    public void Begin() => Begins++;
    public void ReturnToGame() => Returns++;

    public void CopyAndReturnToGame(string text)
    {
        Copied.Add(text);
        Returns++;
    }
}

internal sealed class FakeManualTranslation : IManualTranslationService
{
    public int ReplyCalls;
    public int OriginalCalls;

    public Task<string> TranslateOriginalAsync(string text, CancellationToken ct)
    {
        OriginalCalls++;
        return Task.FromResult("RU:" + text);
    }

    public Task<string> TranslateReplyAsync(string text, CancellationToken ct)
    {
        ReplyCalls++;
        return Task.FromResult("EN:" + text);
    }
}

internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal sealed class MemoryStore : ISettingsStore
{
    public AppSettings Saved = new();
    public AppSettings Load() => new();

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Saved = settings;
        return Task.CompletedTask;
    }
}

internal sealed class Harness
{
    public FakeLoop Loop { get; } = new();
    public FakeReply Reply { get; } = new();
    public FakeManualTranslation Manual { get; } = new();
    public MemoryStore Store { get; } = new();
    public SettingsService Settings { get; }
    public OverlayState Overlay { get; } = new();
    public HudViewModel Hud { get; }
    public ReplyViewModel ReplyVm { get; }
    public FakeModelManager Models { get; } = new();
    public FakeUpdater Updater { get; } = new();
    public ModelsViewModel ModelsVm { get; }
    public UpdateViewModel UpdateVm { get; }

    public Harness()
    {
        Settings = new SettingsService(Store, TimeSpan.FromMilliseconds(10));
        ReplyVm = new ReplyViewModel(Manual, Reply, Settings);
        ModelsVm = new ModelsViewModel(Models, new ImmediateDispatcher());
        UpdateVm = new UpdateViewModel(Updater, new ImmediateDispatcher());
        Hud = new HudViewModel(
            Loop, Settings, Reply, new ImmediateDispatcher(), Overlay,
            new SettingsViewModel(Settings), ModelsVm, UpdateVm, new SourceEditorViewModel(Manual), ReplyVm);
    }
}

internal sealed class FakeModelManager : IModelManager
{
    public IReadOnlyList<ModelStatus> Statuses { get; set; } = [];
    public DownloadState State { get; set; } = new();
    public string? ActiveModelId { get; set; }
    public readonly List<string> Started = [];
    public int Cancels;
    public readonly List<string> Deleted = [];

    public event Action? Changed;

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void StartDownload(string variantId) => Started.Add(variantId);
    public void Cancel() => Cancels++;

    public void Activate(string variantId)
    {
        ActiveModelId = variantId;
        Changed?.Invoke();
    }

    public Task DeleteAsync(string variantId)
    {
        Deleted.Add(variantId);
        return Task.CompletedTask;
    }

    public void Raise() => Changed?.Invoke();
}

internal sealed class FakeUpdater : IAppUpdater
{
    public bool IsInstalled { get; set; } = true;
    public string CurrentVersion => "1.0.0";
    public AppUpdateInfo? Available { get; set; }
    public bool Restarted;
    public bool FailCheck;

    public Task<AppUpdateInfo?> CheckAsync(CancellationToken ct) =>
        FailCheck ? throw new HttpRequestException("offline") : Task.FromResult(Available);

    public Task DownloadAndRestartAsync(IProgress<int>? percent, CancellationToken ct)
    {
        percent?.Report(100);
        Restarted = true;
        return Task.CompletedTask;
    }
}
