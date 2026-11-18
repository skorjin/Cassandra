using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

public abstract record PipelineOutcome
{
    /// <summary>Frame equals the previous one and nothing is pending.</summary>
    public sealed record Idle : PipelineOutcome;

    /// <summary>Frame is still changing (chat is animating); OCR is postponed until it settles.</summary>
    public sealed record Settling : PipelineOutcome;

    /// <summary>OCR ran but produced no new lines.</summary>
    public sealed record NoNewText : PipelineOutcome;

    public sealed record Translated(IReadOnlyList<TranslatedLine> Lines) : PipelineOutcome;
}
