using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tuner.UI.Win.Converters;

/// <summary>
/// Multi-value converter: true when the first two bound values are equal.
/// Used to highlight the string button whose tone is playing.
/// </summary>
public class AreEqualConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;
        if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return false;
        return Equals(values[0], values[1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
