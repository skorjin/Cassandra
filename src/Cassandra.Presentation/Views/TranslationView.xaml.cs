using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Cassandra.Presentation.ViewModels;

namespace Cassandra.Presentation.Views;

/// <summary>
/// Translated lines in a read-only RichTextBox, so any part of them can be selected and copied. Lines are added and
/// removed one by one (never rebuilt) so a selection survives while new messages arrive.
/// </summary>
public partial class TranslationView : UserControl
{
    private HudViewModel? _viewModel;

    public TranslationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Lines.CollectionChanged -= OnLinesChanged;
        }

        _viewModel = e.NewValue as HudViewModel;
        Output.Document.Blocks.Clear();
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Lines.CollectionChanged += OnLinesChanged;
        foreach (var line in _viewModel.Lines)
        {
            Output.Document.Blocks.Add(CreateParagraph(line));
        }
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // follow the chat only if the reader is already at the bottom; someone reading older lines is left alone
        var follow = Output.VerticalOffset + Output.ViewportHeight >= Output.ExtentHeight - 4;
        var blocks = Output.Document.Blocks;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (LineViewModel line in e.NewItems)
                {
                    blocks.Add(CreateParagraph(line));
                }

                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                for (var i = 0; i < e.OldItems.Count && blocks.FirstBlock is { } first; i++)
                {
                    blocks.Remove(first); // lines are only ever dropped from the top
                }

                break;
            default:
                blocks.Clear();
                if (_viewModel is not null)
                {
                    foreach (var line in _viewModel.Lines)
                    {
                        blocks.Add(CreateParagraph(line));
                    }
                }

                break;
        }

        if (follow)
        {
            Dispatcher.BeginInvoke(Output.ScrollToEnd, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private static Paragraph CreateParagraph(LineViewModel line)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4), ToolTip = line.Original };
        if (line.Speaker.Length > 0)
        {
            paragraph.Inlines.Add(new Run(line.Speaker + " ")
            {
                Foreground = (Brush)System.Windows.Application.Current.FindResource("SpeakerBrush"),
                FontWeight = FontWeights.SemiBold,
            });
        }

        paragraph.Inlines.Add(new Run(line.Text));
        return paragraph;
    }
}
