using System.Globalization;
using System.Windows.Data;

namespace JsonDiff.Converters;

/// <summary>True when the bound enum value equals the converter parameter; checks one item of a radio-like menu.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
