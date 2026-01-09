using System.Globalization;
using System.Windows.Data;

namespace Tuner.UI.Win.Converters;

public class CentsToIndicatorPositionConverter : IValueConverter
{
    private const double MaxCents = 50.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double cents)
        {
            // Normalize cents to -1.0 to 1.0 range
            double normalized = Math.Clamp(cents / MaxCents, -1.0, 1.0);

            // Convert to percentage (0.5 = center)
            return (normalized + 1.0) / 2.0;
        }
        return 0.5; // Center
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
