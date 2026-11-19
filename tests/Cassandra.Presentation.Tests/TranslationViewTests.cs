using System.Windows;
using System.Windows.Documents;
using Cassandra.Domain.Models;
using Cassandra.Presentation.ViewModels;
using Cassandra.Presentation.Views;

namespace Cassandra.Presentation.Tests;

public class TranslationViewTests
{
    [Fact]
    public void A_long_chat_scrolls_inside_the_fixed_height_instead_of_being_cut()
    {
        StaRunner.Run(() =>
        {
            var hud = new Harness().Hud;
            var view = new TranslationView { DataContext = hud };
            view.Output.Height = 100;
            for (var i = 0; i < 30; i++)
            {
                hud.Lines.Add(new LineViewModel(new TranslatedLine($"o{i}", $"line {i}", "Nick")));
            }

            view.Measure(new Size(300, 1000));
            view.Arrange(new Rect(0, 0, 300, view.DesiredSize.Height));
            view.UpdateLayout();

            Assert.Equal(100, view.Output.ActualHeight, 1);
            Assert.True(view.Output.ExtentHeight > view.Output.ViewportHeight * 3);
            Assert.Equal(30, view.Output.Document.Blocks.Count);
        });
    }

    [Fact]
    public void Lines_appear_as_selectable_text_and_old_ones_drop_off_the_top()
    {
        StaRunner.Run(() =>
        {
            var hud = new Harness().Hud;
            var view = new TranslationView { DataContext = hud };

            hud.Lines.Add(new LineViewModel(new TranslatedLine("a", "первая", "Ник")));
            hud.Lines.Add(new LineViewModel(new TranslatedLine("b", "вторая")));

            Assert.True(view.Output.IsReadOnly);
            Assert.Equal(2, view.Output.Document.Blocks.Count);
            var text = new TextRange(view.Output.Document.ContentStart, view.Output.Document.ContentEnd).Text;
            Assert.Contains("Ник: первая", text);
            Assert.Contains("вторая", text);

            // the selection of an older line survives while a new one arrives
            var first = (Paragraph)view.Output.Document.Blocks.FirstBlock;
            view.Output.Selection.Select(first.ContentStart, first.ContentEnd);
            hud.Lines.Add(new LineViewModel(new TranslatedLine("c", "третья")));
            Assert.Contains("первая", view.Output.Selection.Text);

            hud.Lines.RemoveAt(0);
            Assert.Equal(2, view.Output.Document.Blocks.Count);
            Assert.DoesNotContain("первая", new TextRange(view.Output.Document.ContentStart, view.Output.Document.ContentEnd).Text);

            hud.Lines.Clear();
            Assert.Empty(view.Output.Document.Blocks);
        });
    }
}
