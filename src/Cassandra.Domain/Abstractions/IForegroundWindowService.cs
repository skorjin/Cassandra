namespace Cassandra.Domain.Abstractions;

public interface IForegroundWindowService
{
    /// <summary>Remembers the window that currently has focus (the game).</summary>
    void Remember();

    /// <summary>Returns focus to the remembered window.</summary>
    void Restore();
}
