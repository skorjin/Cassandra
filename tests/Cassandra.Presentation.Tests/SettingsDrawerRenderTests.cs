using System.Windows;
using Cassandra.Presentation.ViewModels;
using Cassandra.Presentation.Views;

namespace Cassandra.Presentation.Tests;

/// <summary>Loads the real XAML of the drawer for every section, so a broken binding or resource fails here and not on the player's screen.</summary>
public class SettingsDrawerRenderTests
{
    [Fact]
    public void Every_section_of_the_drawer_loads_and_lays_out()
    {
        var sizes = new Dictionary<SettingsSection, double>();

        StaRunner.Run(() =>
        {
            var hud = new Harness().Hud;
            var view = new SettingsDrawerView { DataContext = hud, Width = 340 };
            foreach (var section in Enum.GetValues<SettingsSection>())
            {
                hud.Settings.ShowSectionCommand.Execute(section);
                view.Measure(new Size(340, 2000));
                view.Arrange(new Rect(0, 0, 340, view.DesiredSize.Height));
                view.UpdateLayout();
                sizes[section] = view.DesiredSize.Height;
            }
        });

        Assert.All(sizes.Values, h => Assert.True(h > 40));
    }
}
