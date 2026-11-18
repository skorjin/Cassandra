using System.Runtime.InteropServices;
using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Windows;

namespace Cassandra.Infrastructure.Tests;

public class WindowStylerTests
{
    private const long WsExTransparent = 0x20;
    private const long WsExNoActivate = 0x08000000;
    private const int GwlExStyle = -20;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string cls, string title, int style, int x, int y, int w, int h,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetLong(IntPtr hwnd, int index);

    private static IntPtr NewWindow() => CreateWindowEx(0, "Static", "t", 0, 10, 10, 200, 100, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

    [Fact]
    public void Click_through_and_activation_flags_are_applied_and_removed()
    {
        var hwnd = NewWindow();
        try
        {
            var styler = new Win32WindowStyler();

            styler.SetClickThrough(hwnd, true);
            styler.SetActivatable(hwnd, false);
            var style = GetLong(hwnd, GwlExStyle).ToInt64();
            Assert.NotEqual(0, style & WsExTransparent);
            Assert.NotEqual(0, style & WsExNoActivate);

            styler.SetClickThrough(hwnd, false);
            styler.SetActivatable(hwnd, true);
            style = GetLong(hwnd, GwlExStyle).ToInt64();
            Assert.Equal(0, style & WsExTransparent);
            Assert.Equal(0, style & WsExNoActivate);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }

    [Fact]
    public void Bounds_round_trip_in_physical_pixels_including_negative_coordinates()
    {
        var hwnd = NewWindow();
        try
        {
            var styler = new Win32WindowStyler();
            var wanted = new CaptureRegion(-1500, 321, 480, 136);

            styler.SetBounds(hwnd, wanted);

            Assert.Equal(wanted, styler.GetBounds(hwnd));
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }
}
