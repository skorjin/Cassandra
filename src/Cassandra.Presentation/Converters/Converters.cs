using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Cassandra.Presentation.Converters;

/// <summary>Tint "#RRGGBB" + opacity 0..1 to a semi-transparent brush for the HUD background.</summary>
public sealed class HudBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var color = Colors.Black;
        if (values.Length > 0 && values[0] is string hex)
        {
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (FormatException)
            {
            }
        }

        var opacity = values.Length > 1 && values[1] is double d ? Math.Clamp(d, 0.0, 1.0) : 0.8;
        color.A = (byte)Math.Round(opacity * 255);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string ?? "#000000"));
        }
        catch (FormatException)
        {
            return Brushes.Black;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
