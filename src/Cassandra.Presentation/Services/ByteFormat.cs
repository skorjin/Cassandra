using System.Globalization;

namespace Cassandra.Presentation.Services;

public static class ByteFormat
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>"219 МБ", "1,2 ГБ".</summary>
    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", Ru) + " ГБ",
        >= 1L << 20 => Math.Round(bytes / (double)(1L << 20)).ToString("0", Ru) + " МБ",
        >= 1L << 10 => Math.Round(bytes / (double)(1L << 10)).ToString("0", Ru) + " КБ",
        _ => bytes.ToString(Ru) + " Б",
    };
}
