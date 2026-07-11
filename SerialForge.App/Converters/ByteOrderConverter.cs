using System.Globalization;
using System.Windows.Data;
using SerialForge.Core;

namespace SerialForge.App.Converters;

// Maps nullable ByteOrder <-> ComboBox: null="默认", Little="小端", Big="大端".
public sealed class ByteOrderConverter : IValueConverter
{
    public static readonly string[] Options = { "默认", "小端", "大端" };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ByteOrder.Big ? "大端" : value is ByteOrder.Little ? "小端" : "默认";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? s == "大端" ? ByteOrder.Big : s == "小端" ? ByteOrder.Little : null : null;
}
