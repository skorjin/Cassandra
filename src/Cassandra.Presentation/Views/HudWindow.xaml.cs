using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Cassandra.Application.Settings;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.Services;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Views;

/// <summary>Compact always-on-top HUD. Code-behind only does what XAML cannot: dragging, native window styles, focus.</summary>
public partial class HudWindow : Window
{
    private const double MinHudWidth = 260;
    private const double MaxHudWidth = 1000;
    private const double MaxTextHeight = 900;
    private const double MinTextHeight = 60;
    private const double DefaultTextHeight = 160;

    private readonly HudViewModel _viewModel;
    private readonly OverlayState _overlay;
    private readonly IWindowStyler _styler;
    private readonly ISettingsService _settings;
    private readonly IScreenLayout _screens;
    private IntPtr _hwnd;
    private bool _textArmed;

    public HudWindow(HudViewModel viewModel, OverlayState overlay, IWindowStyler styler, ISettingsService settings, IScreenLayout screens)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _overlay = overlay;
        _styler = styler;
        _settings = settings;
        _screens = screens;
        DataContext = viewModel;

        var bounds = settings.Current.Hud;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = Math.Clamp(bounds.Width, MinHudWidth, MaxHudWidth);
        Translation.Output.Height = settings.Current.HudTextHeight >= MinTextHeight
            ? Math.Min(settings.Current.HudTextHeight, MaxTextHeight)
            : DefaultTextHeight;
        ClampToScreen();

        RightEdge.DragDelta += (_, e) => Resize(e.HorizontalChange, 0);
        BottomEdge.DragDelta += (_, e) => Resize(0, e.VerticalChange);
        CornerGrip.DragDelta += (_, e) => Resize(e.HorizontalChange, e.VerticalChange);
        RightEdge.DragCompleted += (_, _) => SaveBounds();
        BottomEdge.DragCompleted += (_, _) => SaveBounds();
        CornerGrip.DragCompleted += (_, _) => SaveBounds();

        Header.MouseLeftButtonDown += OnHeaderMouseDown;
        WireTextSelection();
        LocationChanged += (_, _) => SaveBounds();
        SizeChanged += (_, _) => ClampToScreen();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.ReplyFocusRequested += OnReplyFocusRequested;
        _viewModel.CloseRequested += () => System.Windows.Application.Current.Shutdown();
        _overlay.PropertyChanged += OnOverlayChanged;
        _overlay.LayoutResetRequested += () => MoveOntoPrimaryMonitor(force: true);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _styler.HideFromTaskSwitcher(_hwnd);
        _styler.ExcludeFromCapture(_hwnd);
        _styler.SetActivatable(_hwnd, false);
        _styler.SetClickThrough(_hwnd, _overlay.IsLocked);
        MoveOntoPrimaryMonitor(force: false); // a saved spot on a monitor that is gone must not hide the window
    }

    /// <summary>Puts the HUD on the primary monitor: always when forced, otherwise only if it is out of reach.</summary>
    private void MoveOntoPrimaryMonitor(bool force)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var bounds = _styler.GetBounds(_hwnd);
        if (force || !WindowPlacement.IsReachable(bounds, _screens.WorkAreas))
        {
            _styler.SetBounds(_hwnd, WindowPlacement.HudDefault(bounds, _screens.PrimaryWorkArea));
            SaveBounds();
        }
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

    /// <summary>
    /// The HUD never takes keyboard focus from the game, except while the player works with its text. When the mouse
    /// is over the translation the window becomes activatable, so a click selects text and Ctrl+C reaches it; it goes
    /// back to "never activate" as soon as the mouse leaves or focus is lost, and focus returns to the game after a copy.
    /// </summary>
    private void WireTextSelection()
    {
        var output = Translation.Output;
        output.MouseEnter += (_, _) => ArmTextSelection();
        output.MouseLeave += (_, _) => DisarmTextSelection(keepWhileActive: true);
        output.LostKeyboardFocus += (_, _) =>
        {
            if (!output.IsKeyboardFocusWithin)
            {
                DisarmTextSelection(keepWhileActive: false);
            }
        };
        output.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _textArmed)
            {
                FinishTextSelection();
                e.Handled = true;
            }
        };
        DataObject.AddCopyingHandler(output, (_, _) => Dispatcher.BeginInvoke(FinishTextSelection));
        Deactivated += (_, _) => DisarmTextSelection(keepWhileActive: false);
    }

    private void ArmTextSelection()
    {
        if (_textArmed || _viewModel.IsEditorOpen || _hwnd == IntPtr.Zero || IsActive)
        {
            return;
        }

        _textArmed = true;
        _viewModel.BeginTextSelection(); // remember the game window while it is still in front
        _styler.SetActivatable(_hwnd, true);
    }

    private void DisarmTextSelection(bool keepWhileActive)
    {
        if (!_textArmed || (keepWhileActive && IsActive))
        {
            return;
        }

        _textArmed = false;
        if (!_viewModel.IsEditorOpen)
        {
            _styler.SetActivatable(_hwnd, false);
        }
    }

    /// <summary>After a copy or Escape: stop being focusable and give the keyboard back to the game.</summary>
    private void FinishTextSelection()
    {
        DisarmTextSelection(keepWhileActive: false);
        if (!_viewModel.IsEditorOpen)
        {
            _viewModel.EndTextSelection();
        }
    }
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HudViewModel.IsEditorOpen) && _hwnd != IntPtr.Zero)
        {
            // while editing the HUD must accept keyboard focus; otherwise it never steals it from the game
            _styler.SetActivatable(_hwnd, _viewModel.IsEditorOpen);
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

    /// <summary>Vertical resizing changes the height of the text area; longer chats scroll inside it.</summary>
    private void Resize(double dx, double dy)
    {
        Width = Math.Clamp(Width + dx, MinHudWidth, MaxHudWidth);
        var output = Translation.Output;
        output.Height = Math.Clamp(output.Height + dy, MinTextHeight, MaxTextHeight);
    }

    private void SaveBounds() =>
        _settings.Update(s => s with
        {
            Hud = new WindowBounds(Left, Top, Width, ActualHeight),
            HudTextHeight = Translation.Output.Height,
        });

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
