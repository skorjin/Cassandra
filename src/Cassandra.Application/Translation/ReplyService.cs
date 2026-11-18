using Cassandra.Domain.Abstractions;

namespace Cassandra.Application.Translation;

public sealed class ReplyService : IReplyService
{
    private readonly IClipboardService _clipboard;
    private readonly IForegroundWindowService _foreground;

    public ReplyService(IClipboardService clipboard, IForegroundWindowService foreground)
    {
        _clipboard = clipboard;
        _foreground = foreground;
    }

    public void Begin() => _foreground.Remember();

    public void ReturnToGame() => _foreground.Restore();

    public void CopyAndReturnToGame(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _clipboard.SetText(text);
        _foreground.Restore();
    }
}
