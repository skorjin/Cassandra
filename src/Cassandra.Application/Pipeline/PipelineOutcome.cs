using Cassandra.Domain.Models;

namespace Cassandra.Application.Pipeline;

public abstract record PipelineOutcome
{
    /// <summary>Frame equals the previous one and nothing is pending.</summary>
    public sealed record Idle : PipelineOutcome;

    /// <summary>Frame is still changing (chat is animating); OCR is postponed until it settles.</summary>
    public sealed record Settling : PipelineOutcome;

    /// <summary>Everything the frame shows right now, translated. Replaces whatever was displayed before.</summary>
    public sealed record Translated(IReadOnlyList<TranslatedLine> Lines) : PipelineOutcome;
}
