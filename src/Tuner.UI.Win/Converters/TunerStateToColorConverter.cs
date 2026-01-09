using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Tuner.AppContracts;

namespace Tuner.UI.Win.Converters;

public class TunerStateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InTuneBrush = new(Color.FromRgb(0, 200, 83));     // Green
    private static readonly SolidColorBrush FlatBrush = new(Color.FromRgb(255, 152, 0));      // Orange
    private static readonly SolidColorBrush SharpBrush = new(Color.FromRgb(255, 152, 0));     // Orange
    private static readonly SolidColorBrush UnstableBrush = new(Color.FromRgb(158, 158, 158)); // Gray
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(97, 97, 97));    // Dark gray

    static TunerStateToColorConverter()
    {
        InTuneBrush.Freeze();
        FlatBrush.Freeze();
        SharpBrush.Freeze();
        UnstableBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TunerState state)
        {
            return state switch
            {
                TunerState.InTune => InTuneBrush,
                TunerState.Flat => FlatBrush,
                TunerState.Sharp => SharpBrush,
                TunerState.Unstable => UnstableBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
