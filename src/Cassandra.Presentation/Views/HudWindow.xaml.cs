using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Views;

/// <summary>Compact always-on-top HUD. Code-behind only does what XAML cannot: dragging, native window styles, focus.</summary>
public partial class HudWindow : Window
{
    private readonly HudViewModel _viewModel;
    private readonly OverlayState _overlay;
    private readonly IWindowStyler _styler;
    private readonly ISettingsService _settings;
    private IntPtr _hwnd;

    public HudWindow(HudViewModel viewModel, OverlayState overlay, IWindowStyler styler, ISettingsService settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _overlay = overlay;
        _styler = styler;
        _settings = settings;
        DataContext = viewModel;

        var bounds = settings.Current.Hud;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = Math.Clamp(bounds.Width, 280, 640);
        ClampToScreen();

        Header.MouseLeftButtonDown += OnHeaderMouseDown;
        LocationChanged += (_, _) => SaveBounds();
        SizeChanged += (_, _) => ClampToScreen();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.ReplyFocusRequested += OnReplyFocusRequested;
        _viewModel.CloseRequested += () => System.Windows.Application.Current.Shutdown();
        _overlay.PropertyChanged += OnOverlayChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _styler.HideFromTaskSwitcher(_hwnd);
        _styler.ExcludeFromCapture(_hwnd);
        _styler.SetActivatable(_hwnd, false);
        _styler.SetClickThrough(_hwnd, _overlay.IsLocked);
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // button released before the drag started
            }
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HudViewModel.IsPaused) && _hwnd != IntPtr.Zero)
        {
            // while editing the HUD must accept keyboard focus; otherwise it never steals it from the game
            _styler.SetActivatable(_hwnd, _viewModel.IsPaused);
        }
    }

    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayState.IsLocked) && _hwnd != IntPtr.Zero)
        {
            _styler.SetClickThrough(_hwnd, _overlay.IsLocked);
        }
        else if (e.PropertyName == nameof(OverlayState.IsVisible))
        {
            Visibility = _overlay.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnReplyFocusRequested() => Dispatcher.BeginInvoke(() =>
    {
        _styler.BringToForeground(_hwnd);
        Activate();
        ReplyPanel.FocusInput();
    }, System.Windows.Threading.DispatcherPriority.Input);

    private void SaveBounds() =>
        _settings.Update(s => s with { Hud = new WindowBounds(Left, Top, Width, ActualHeight) });

    /// <summary>Keeps the HUD inside the visible desktop, e.g. when the drawer grows it past the bottom edge.</summary>
    private void ClampToScreen()
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;
        var height = double.IsNaN(ActualHeight) || ActualHeight == 0 ? 120 : ActualHeight;

        Left = Math.Clamp(Left, left, Math.Max(left, right - Width));
        Top = Math.Clamp(Top, top, Math.Max(top, bottom - height));
    }
}
