using System.Runtime.InteropServices;
using System.Windows;
using Cassandra.Domain.Abstractions;

namespace Cassandra.Presentation.Services;

public sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        // the clipboard can be locked by another process for a moment
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
    }
}
