using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;
using Cassandra.Presentation.State;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Views;

/// <summary>
/// Dashed, draggable and resizable rectangle placed over the game chat. Its inner area is what gets captured.
/// Geometry is handled in physical pixels through the window styler, so per-monitor DPI never shifts the capture area.
/// </summary>
public partial class CaptureFrameWindow : Window
{
    private const int InsetPx = 3; // keeps the dashed border itself out of the captured pixels
    private const int MinWidthPx = 120;
    private const int MinHeightPx = 40;
    private const int WmDpiChanged = 0x02E0;

    private readonly CaptureFrameViewModel _viewModel;
    private readonly IWindowStyler _styler;
    private IntPtr _hwnd;
    private CaptureRegion _bounds;
    private CaptureRegion _resizeStartBounds;
    private Point _resizeStartCursor;

    public CaptureFrameWindow(CaptureFrameViewModel viewModel, IWindowStyler styler)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _styler = styler;
        DataContext = viewModel;
        _bounds = Inflate(viewModel.Region);

        MouseLeftButtonDown += OnDragStart;
        WireThumb(TopLeft, left: true, top: true);
        WireThumb(TopRight, left: false, top: true);
        WireThumb(BottomLeft, left: true, top: false);
        WireThumb(BottomRight, left: false, top: false);

        viewModel.Overlay.PropertyChanged += OnOverlayChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        _styler.HideFromTaskSwitcher(_hwnd);
        _styler.ExcludeFromCapture(_hwnd);
        _styler.SetActivatable(_hwnd, false);
        _styler.SetBounds(_hwnd, _bounds);
        ApplyVisibility();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDpiChanged)
        {
            // WPF would resize the window to the new monitor's DPI; the capture rectangle must not move.
            handled = true;
            _styler.SetBounds(_hwnd, _bounds);
        }

        return IntPtr.Zero;
    }

    private void WireThumb(Thumb thumb, bool left, bool top)
    {
        thumb.DragStarted += (_, _) =>
        {
            _resizeStartBounds = _bounds;
            _resizeStartCursor = CursorOnScreen();
        };
        thumb.DragDelta += (_, _) => Resize(left, top);
        thumb.DragCompleted += (_, _) => PublishRegion();
    }

    private void Resize(bool left, bool top)
    {
        var cursor = CursorOnScreen();
        var dx = (int)Math.Round(cursor.X - _resizeStartCursor.X);
        var dy = (int)Math.Round(cursor.Y - _resizeStartCursor.Y);
        var b = _resizeStartBounds;

        var x = b.X;
        var y = b.Y;
        var width = left ? b.Width - dx : b.Width + dx;
        var height = top ? b.Height - dy : b.Height + dy;

        width = Math.Max(width, MinWidthPx);
        height = Math.Max(height, MinHeightPx);
        if (left)
        {
            x = b.X + b.Width - width;
        }

        if (top)
        {
            y = b.Y + b.Height - height;
        }

        _bounds = new CaptureRegion(x, y, width, height);
        _styler.SetBounds(_hwnd, _bounds);
    }

    private Point CursorOnScreen() => PointToScreen(Mouse.GetPosition(this));

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            _bounds = _styler.GetBounds(_hwnd);
            PublishRegion();
        }
        catch (InvalidOperationException)
        {
            // button released before the drag started
        }
    }

    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayState.IsLocked) or nameof(OverlayState.IsVisible)
            or nameof(OverlayState.CanEditFrame))
        {
            ApplyVisibility();
        }
    }

    /// <summary>
    /// The frame is shown while unlocked and the overlay is visible. Only while paused (or in edit mode) it catches
    /// the mouse; during live translation clicks and scrolling go through it to the game chat.
    /// </summary>
    private void ApplyVisibility()
    {
        var overlay = _viewModel.Overlay;
        Visibility = overlay.IsVisible && !overlay.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        var editable = overlay.CanEditFrame && !overlay.IsLocked;
        Cursor = editable ? Cursors.SizeAll : Cursors.Arrow;
        var grips = editable ? Visibility.Visible : Visibility.Collapsed;
        TopLeft.Visibility = TopRight.Visibility = BottomLeft.Visibility = BottomRight.Visibility = grips;
        Border.Opacity = editable ? 1 : 0.45;
        if (_hwnd != IntPtr.Zero)
        {
            _styler.SetClickThrough(_hwnd, !editable);
        }
    }

    private void PublishRegion() => _viewModel.UpdateRegion(Deflate(_bounds));

    private static CaptureRegion Inflate(CaptureRegion r) =>
        new(r.X - InsetPx, r.Y - InsetPx, Math.Max(MinWidthPx, r.Width + InsetPx * 2), Math.Max(MinHeightPx, r.Height + InsetPx * 2));

    private static CaptureRegion Deflate(CaptureRegion b) =>
        new(b.X + InsetPx, b.Y + InsetPx, b.Width - InsetPx * 2, b.Height - InsetPx * 2);
}
