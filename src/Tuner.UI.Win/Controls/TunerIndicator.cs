using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Tuner.AppContracts;

namespace Tuner.UI.Win.Controls;

/// <summary>
/// Custom tuner indicator control that displays pitch offset visually.
/// </summary>
public class TunerIndicator : Control
{
    private const double MaxCentsDisplay = 50.0;

    static TunerIndicator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TunerIndicator),
            new FrameworkPropertyMetadata(typeof(TunerIndicator)));
    }

    public static readonly DependencyProperty CentsOffsetProperty =
        DependencyProperty.Register(
            nameof(CentsOffset),
            typeof(double),
            typeof(TunerIndicator),
            new PropertyMetadata(0.0, OnCentsOffsetChanged));

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(TunerState),
            typeof(TunerIndicator),
            new PropertyMetadata(TunerState.Unknown, OnStateChanged));

    public double CentsOffset
    {
        get => (double)GetValue(CentsOffsetProperty);
        set => SetValue(CentsOffsetProperty, value);
    }

    public TunerState State
    {
        get => (TunerState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private Canvas? _canvas;
    private Ellipse? _indicator;
    private Rectangle? _centerLine;
    private Path? _flatArrow;
    private Path? _sharpArrow;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        _indicator = GetTemplateChild("PART_Indicator") as Ellipse;
        _centerLine = GetTemplateChild("PART_CenterLine") as Rectangle;
        _flatArrow = GetTemplateChild("PART_FlatArrow") as Path;
        _sharpArrow = GetTemplateChild("PART_SharpArrow") as Path;

        UpdateIndicator();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateIndicator();
    }

    private static void OnCentsOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TunerIndicator indicator)
        {
            indicator.UpdateIndicator();
        }
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TunerIndicator indicator)
        {
            indicator.UpdateIndicator();
        }
    }

    private void UpdateIndicator()
    {
        if (_canvas == null || _indicator == null)
            return;

        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        // Calculate indicator position
        double normalizedOffset = Math.Clamp(CentsOffset / MaxCentsDisplay, -1.0, 1.0);
        double centerX = width / 2;
        double indicatorX = centerX + (normalizedOffset * (width / 2 - 30));

        // Position indicator
        Canvas.SetLeft(_indicator, indicatorX - _indicator.Width / 2);
        Canvas.SetTop(_indicator, height / 2 - _indicator.Height / 2);

        // Update indicator color based on state
        _indicator.Fill = State switch
        {
            TunerState.InTune => new SolidColorBrush(Color.FromRgb(0, 200, 83)),     // Green
            TunerState.Flat => new SolidColorBrush(Color.FromRgb(255, 152, 0)),       // Orange
            TunerState.Sharp => new SolidColorBrush(Color.FromRgb(255, 152, 0)),      // Orange
            TunerState.Unstable => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
            _ => new SolidColorBrush(Color.FromRgb(97, 97, 97))                       // Dark gray
        };

        // Update arrow visibility
        if (_flatArrow != null)
        {
            _flatArrow.Visibility = State == TunerState.Flat ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_sharpArrow != null)
        {
            _sharpArrow.Visibility = State == TunerState.Sharp ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
