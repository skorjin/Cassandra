using System.Windows;
using Cassandra.Application.Detection;
using Cassandra.Application.Models;
using Cassandra.Application.Pipeline;
using Cassandra.Application.Settings;
using Cassandra.Application.Translation;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Capture;
using Cassandra.Infrastructure.Models;
using Cassandra.Infrastructure.Ocr;
using Cassandra.Infrastructure.Settings;
using Cassandra.Infrastructure.Translation;
using Cassandra.Infrastructure.Updates;
using Cassandra.Infrastructure.Windows;
using Cassandra.Presentation.Services;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;
using Cassandra.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cassandra.App;

/// <summary>Composition root: the only place where layers are wired together.</summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "Cassandra.Overlay.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _services = BuildServices();

        var hud = _services.GetRequiredService<HudWindow>();
        var frame = _services.GetRequiredService<CaptureFrameWindow>();
        frame.Show();
        hud.Show();

        _services.GetRequiredService<HotkeyCoordinator>().Start();
        _services.GetRequiredService<ICaptureLoop>().Start();

        _ = StartBackgroundWorkAsync(_services);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IScreenCapturer, GdiScreenCapturer>();
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
        services.AddSingleton<IWindowStyler, Win32WindowStyler>();
        services.AddSingleton<IForegroundWindowService, Win32ForegroundWindowService>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<ModelStorage>();
        services.AddSingleton<IModelStore>(sp => sp.GetRequiredService<ModelStorage>());
        services.AddSingleton<IModelDownloader, HttpModelDownloader>();
        services.AddSingleton<IModelCatalog, ModelCatalogLoader>();
        services.AddSingleton<IAppUpdater>(_ => new VelopackAppUpdater());
        services.AddSingleton<ITextTranslator>(sp =>
        {
            var store = sp.GetRequiredService<IModelStore>();
            var active = sp.GetRequiredService<IActiveModelProvider>();
            return new OnnxMarianTranslator(() => active.ActiveModelId is { } id ? store.GetInstalledDirectory(id) : null);
        });

        // Application
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IActiveModelProvider, ActiveModelProvider>();
        services.AddSingleton<IModelManager, ModelManager>();
        services.AddSingleton<IFrameChangeDetector, FrameChangeDetector>();
        services.AddSingleton<IChatTextDiffer, ChatTextDiffer>();
        services.AddSingleton<TranslationPipeline>();
        services.AddSingleton<ICaptureLoop, CaptureLoopService>();
        services.AddSingleton<IManualTranslationService, ManualTranslationService>();
        services.AddSingleton<IReplyService, ReplyService>();

        // Presentation
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IClipboardService, WpfClipboardService>();
        services.AddSingleton<OverlayState>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ModelsViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<SourceEditorViewModel>();
        services.AddSingleton<ReplyViewModel>();
        services.AddSingleton<HudViewModel>();
        services.AddSingleton<CaptureFrameViewModel>();
        services.AddSingleton<HotkeyCoordinator>();
        services.AddSingleton<HudWindow>();
        services.AddSingleton<CaptureFrameWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>Loads the model catalog, warms the translator up and quietly checks for an app update.</summary>
    private static async Task StartBackgroundWorkAsync(IServiceProvider services)
    {
        var models = services.GetRequiredService<ModelsViewModel>();
        await models.InitializeAsync();

        var settings = services.GetRequiredService<ISettingsService>();
        if (settings.Current.ActiveModelId is not null)
        {
            // load the chat direction now so the first chat line is not slow; the reply direction loads on first use
            var translator = services.GetRequiredService<ITextTranslator>();
            try
            {
                await translator.TranslateAsync("ok", settings.Current.Languages, CancellationToken.None);
            }
            catch (Exception)
            {
                // reported by the capture loop on first real use
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(8));
        await services.GetRequiredService<UpdateViewModel>().CheckSilentlyAsync();
    }
}
