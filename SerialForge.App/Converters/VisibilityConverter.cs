using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SerialForge.App.Converters;

public sealed class VisibilityConverter : IValueConverter
{
    public static readonly VisibilityConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
