using Velopack;

namespace Cassandra.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run first: handles install / update / uninstall hooks before any UI is created.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
