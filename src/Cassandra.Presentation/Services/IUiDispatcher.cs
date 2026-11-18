namespace Cassandra.Presentation.Services;

/// <summary>Marshals work from background threads (capture loop, hotkeys) to the UI thread.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
