using Cassandra.Domain.Models;

namespace Cassandra.Presentation.ViewModels;

public sealed class LineViewModel
{
    public LineViewModel(TranslatedLine line)
    {
        Speaker = line.Speaker is { Length: > 0 } s ? s + ":" : string.Empty;
        Text = line.Translated;
        Original = line.Original;
        Model = line;
    }

    public string Speaker { get; }
    public string Text { get; }
    public string Original { get; }
    public TranslatedLine Model { get; }
}
