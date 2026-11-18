namespace Cassandra.Infrastructure;

/// <summary>Locates read-only resources shipped with the app (OCR data) next to the executable or, in development, up the tree.</summary>
public static class AppPaths
{
    public static string Tessdata => Locate("tessdata");

    public static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cassandra", "settings.json");

    private static string Locate(string folder)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, folder);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, folder);
    }
}
