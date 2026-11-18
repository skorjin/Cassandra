namespace Cassandra.Application.Translation;

public interface IReplyService
{
    /// <summary>Call when reply mode opens: remembers the game window.</summary>
    void Begin();

    /// <summary>Puts the text on the clipboard and returns focus to the game.</summary>
    void CopyAndReturnToGame(string text);

    /// <summary>Gives focus back to the game without copying anything.</summary>
    void ReturnToGame();
}
