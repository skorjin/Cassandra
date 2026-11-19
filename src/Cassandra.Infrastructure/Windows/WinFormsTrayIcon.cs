using System.Drawing;
using System.Windows.Forms;
using Cassandra.Domain.Abstractions;

namespace Cassandra.Infrastructure.Windows;

/// <summary>Notification-area icon through WinForms NotifyIcon. The icon comes from the executable itself.</summary>
public sealed class WinFormsTrayIcon : ITrayIcon
{
    private NotifyIcon? _icon;
    private ContextMenuStrip? _menu;

    public void Show(string tooltip, Func<IReadOnlyList<TrayMenuItem>> menu, Action onDoubleClick)
    {
        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) =>
        {
            _menu.Items.Clear();
            foreach (var item in menu())
            {
                if (item.IsSeparator)
                {
                    _menu.Items.Add(new ToolStripSeparator());
                    continue;
                }

                var entry = new ToolStripMenuItem(item.Text) { Checked = item.IsChecked };
                var action = item.Invoke;
                entry.Click += (_, _) => action?.Invoke();
                _menu.Items.Add(entry);
            }
        };

        _icon = new NotifyIcon
        {
            Text = tooltip,
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => onDoubleClick();
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path && Icon.ExtractAssociatedIcon(path) is { } icon)
            {
                return icon;
            }
        }
        catch (Exception)
        {
            // fall back to the generic icon below
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false; // otherwise a ghost icon stays until the mouse passes over it
            _icon.Dispose();
        }

        _menu?.Dispose();
    }
}
