using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

public interface ICaptureLoop : IDisposable
{
    PipelineState State { get; }
    event Action<PipelineState>? StateChanged;
    event Action<IReadOnlyList<TranslatedLine>>? LinesTranslated;
    event Action<Exception>? Failed;
    void Start();
    Task PauseAsync();
    void Resume();
}
