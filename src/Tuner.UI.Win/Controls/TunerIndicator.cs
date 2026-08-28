using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tuner.AppContracts;

namespace Tuner.UI.Win.Controls;

/// <summary>
/// Vertical tuner indicator: a fine needle moves UP when the string is sharp
/// and DOWN when it is flat, against a scale that is zoomed in near the target
/// (±10 cents takes up half the scale) so small corrections are easy to see.
/// A large arrow above (tune up) or below (tune down) the scale shows which
/// way to turn the peg.
/// </summary>
public class TunerIndicator : FrameworkElement
{
    // Scale geometry (cents)
    private const double MaxCents = 50.0;
    private const double FineCents = 10.0;     // |cents| <= FineCents uses the inner half of the scale
    private const double FineFraction = 0.5;   // fraction of the half-height given to the fine zone

    // Layout (device-independent pixels)
    private const double ArrowZone = 60;       // room above/below the track for the big arrows
    private const double TrackWidth = 14;
    private const double NeedleHalfWidth = 44;
    private const double LabelGap = 30;

    private static readonly TimeSpan NeedleAnimation = TimeSpan.FromMilliseconds(70);

    static TunerIndicator()
    {
        // Needs a size to draw into; default so it never collapses in a StackPanel.
        MinHeightProperty.OverrideMetadata(typeof(TunerIndicator), new FrameworkPropertyMetadata(220.0));
        MinWidthProperty.OverrideMetadata(typeof(TunerIndicator), new FrameworkPropertyMetadata(220.0));
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
            new FrameworkPropertyMetadata(TunerState.Unknown, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InTuneToleranceProperty =
        DependencyProperty.Register(
            nameof(InTuneTolerance),
            typeof(double),
            typeof(TunerIndicator),
            new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// The cents value the needle is currently drawn at. Animated towards
    /// <see cref="CentsOffset"/> so the needle glides instead of stepping.
    /// </summary>
    private static readonly DependencyProperty DisplayCentsProperty =
        DependencyProperty.Register(
            "DisplayCents",
            typeof(double),
            typeof(TunerIndicator),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public double InTuneTolerance
    {
        get => (double)GetValue(InTuneToleranceProperty);
        set => SetValue(InTuneToleranceProperty, value);
    }

    private double DisplayCents => (double)GetValue(DisplayCentsProperty);

    private static void OnCentsOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TunerIndicator indicator)
            return;

        double target = Math.Clamp((double)e.NewValue, -MaxCents, MaxCents);

        // Off-screen (tests, design time) there is no animation clock; jump.
        if (PresentationSource.FromVisual(indicator) == null)
        {
            indicator.BeginAnimation(DisplayCentsProperty, null);
            indicator.SetValue(DisplayCentsProperty, target);
            return;
        }

        var animation = new DoubleAnimation(target, NeedleAnimation)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        indicator.BeginAnimation(DisplayCentsProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    // ---- Rendering -----------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var palette = Palette.Resolve(this);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        double centerX = Math.Round(width / 2);
        double trackTop = ArrowZone;
        double trackBottom = height - ArrowZone;
        double trackHeight = trackBottom - trackTop;
        if (trackHeight < 40)
            return;

        double centerY = trackTop + trackHeight / 2;
        double halfHeight = trackHeight / 2;

        double CentsToY(double cents) => centerY - CentsToOffset(cents, halfHeight);

        bool hasPitch = State is TunerState.Flat or TunerState.Sharp or TunerState.InTune or TunerState.Unstable;
        bool settled = State is TunerState.Flat or TunerState.Sharp or TunerState.InTune;

        // Track
        var trackRect = new Rect(centerX - TrackWidth / 2, trackTop, TrackWidth, trackHeight);
        dc.DrawRoundedRectangle(palette.Surface, null, trackRect, TrackWidth / 2, TrackWidth / 2);

        // In-tune band (±tolerance)
        double bandTop = CentsToY(InTuneTolerance);
        double bandBottom = CentsToY(-InTuneTolerance);
        var bandBrush = new SolidColorBrush(Color.FromArgb(State == TunerState.InTune ? (byte)200 : (byte)90, palette.InTuneColor.R, palette.InTuneColor.G, palette.InTuneColor.B));
        bandBrush.Freeze();
        dc.DrawRectangle(bandBrush, null, new Rect(centerX - TrackWidth / 2, bandTop, TrackWidth, bandBottom - bandTop));

        // Ticks + labels
        var minorPen = new Pen(palette.Border, 1);
        var majorPen = new Pen(palette.TextSecondary, 1);
        var centerPen = new Pen(palette.InTune, 2);
        minorPen.Freeze(); majorPen.Freeze(); centerPen.Freeze();

        // Fine zone: every cent minor, every 5 major
        for (int c = -(int)FineCents; c <= (int)FineCents; c++)
        {
            bool major = c % 5 == 0;
            DrawTick(dc, centerX, CentsToY(c), major ? 22 : 10, c == 0 ? centerPen : major ? majorPen : minorPen);
            if (major && c != 0)
                DrawLabel(dc, palette.TextSecondary, pixelsPerDip, FormatCents(c), centerX - TrackWidth / 2 - 22 - LabelGap, CentsToY(c), 12);
        }
        // Coarse zone: tick every 10 cents, label only the ends
        for (int c = 20; c <= (int)MaxCents; c += 10)
        {
            foreach (int sign in new[] { -1, 1 })
            {
                double y = CentsToY(sign * c);
                bool labelled = c == (int)MaxCents;
                DrawTick(dc, centerX, y, labelled ? 22 : 14, labelled ? majorPen : minorPen);
                if (labelled)
                    DrawLabel(dc, palette.TextSecondary, pixelsPerDip, FormatCents(sign * c), centerX - TrackWidth / 2 - 22 - LabelGap, y, 12);
            }
        }
        // Zero label
        DrawLabel(dc, palette.InTune, pixelsPerDip, "0", centerX - TrackWidth / 2 - 22 - LabelGap, centerY, 12, bold: true);

        // Zone captions on the right
        DrawLabel(dc, palette.TextSecondary, pixelsPerDip, "SHARP", centerX + TrackWidth / 2 + 22 + LabelGap, CentsToY(MaxCents), 11, alignLeft: true);
        DrawLabel(dc, palette.TextSecondary, pixelsPerDip, "FLAT", centerX + TrackWidth / 2 + 22 + LabelGap, CentsToY(-MaxCents), 11, alignLeft: true);

        // Arrows: FLAT -> tune UP (arrow above), SHARP -> tune DOWN (arrow below)
        // Tip is the far end; the tail stops ArrowGap short of the track.
        const double arrowW = 44, arrowH = 26, arrowTail = 8, arrowGap = 14;
        DrawArrow(dc, centerX, trackTop - arrowGap - arrowH - arrowTail, arrowW, arrowH, up: true,
            State == TunerState.Flat ? palette.Flat : palette.Neutral, active: State == TunerState.Flat);
        DrawArrow(dc, centerX, trackBottom + arrowGap + arrowH + arrowTail, arrowW, arrowH, up: false,
            State == TunerState.Sharp ? palette.Sharp : palette.Neutral, active: State == TunerState.Sharp);

        // Needle
        if (!hasPitch)
            return;

        double needleY = Math.Round(CentsToY(DisplayCents)) + 0.5;
        Brush needleBrush = State switch
        {
            TunerState.InTune => palette.InTune,
            TunerState.Flat => palette.Flat,
            TunerState.Sharp => palette.Sharp,
            _ => palette.Unstable
        };
        var needlePen = new Pen(needleBrush, settled ? 2 : 1.5);
        needlePen.Freeze();
        dc.DrawLine(needlePen, new Point(centerX - NeedleHalfWidth, needleY), new Point(centerX + NeedleHalfWidth, needleY));

        // Pointer chevrons at both ends so the line reads as a needle
        var ptr = Geometry.Parse("M 0,0 L 8,-5 L 8,5 Z");
        dc.PushTransform(new TranslateTransform(centerX + NeedleHalfWidth, needleY));
        dc.DrawGeometry(needleBrush, null, ptr);
        dc.Pop();
        dc.PushTransform(new TranslateTransform(centerX - NeedleHalfWidth, needleY));
        dc.PushTransform(new ScaleTransform(-1, 1));
        dc.DrawGeometry(needleBrush, null, ptr);
        dc.Pop();
        dc.Pop();

        // Live cents readout riding the needle
        string readout = settled ? FormatCents(DisplayCents, decimals: 1) + "¢" : "…";
        DrawLabel(dc, needleBrush, pixelsPerDip, readout, centerX + NeedleHalfWidth + 14, needleY, 14, bold: true, alignLeft: true);
    }

    /// <summary>
    /// Piecewise-linear scale: the inner ±FineCents occupies FineFraction of the
    /// half-height, the rest spreads over the remainder. Positive is upward.
    /// </summary>
    private static double CentsToOffset(double cents, double halfHeight)
    {
        double c = Math.Clamp(cents, -MaxCents, MaxCents);
        double a = Math.Abs(c);
        double fineSpan = halfHeight * FineFraction;
        double offset = a <= FineCents
            ? a / FineCents * fineSpan
            : fineSpan + (a - FineCents) / (MaxCents - FineCents) * (halfHeight - fineSpan);
        return Math.Sign(c) * offset;
    }

    private static void DrawTick(DrawingContext dc, double centerX, double y, double halfLength, Pen pen)
    {
        y = Math.Round(y) + 0.5;
        dc.DrawLine(pen, new Point(centerX - TrackWidth / 2 - halfLength, y), new Point(centerX - TrackWidth / 2 - 4, y));
        dc.DrawLine(pen, new Point(centerX + TrackWidth / 2 + 4, y), new Point(centerX + TrackWidth / 2 + halfLength, y));
    }

    private static void DrawArrow(DrawingContext dc, double centerX, double tipY, double w, double h, bool up, Brush brush, bool active)
    {
        double dir = up ? 1 : -1;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(centerX, tipY), true, true);
            ctx.LineTo(new Point(centerX + w / 2, tipY + dir * h), true, false);
            ctx.LineTo(new Point(centerX + w / 6, tipY + dir * h), true, false);
            ctx.LineTo(new Point(centerX + w / 6, tipY + dir * (h + 8)), true, false);
            ctx.LineTo(new Point(centerX - w / 6, tipY + dir * (h + 8)), true, false);
            ctx.LineTo(new Point(centerX - w / 6, tipY + dir * h), true, false);
            ctx.LineTo(new Point(centerX - w / 2, tipY + dir * h), true, false);
        }
        geo.Freeze();

        if (active)
        {
            dc.DrawGeometry(brush, null, geo);
        }
        else
        {
            var pen = new Pen(brush, 1.5);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }

    private static void DrawLabel(DrawingContext dc, Brush brush, double pixelsPerDip, string text, double x, double y,
        double size, bool bold = false, bool alignLeft = false)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            pixelsPerDip);
        double left = alignLeft ? x : x - ft.Width;
        dc.DrawText(ft, new Point(left, y - ft.Height / 2));
    }

    private static string FormatCents(double cents, int decimals = 0)
    {
        string body = Math.Abs(cents).ToString("F" + decimals, CultureInfo.CurrentUICulture);
        if (Math.Abs(cents) < 0.05 && decimals > 0) return body;
        return cents < 0 ? "−" + body : "+" + body;
    }

    /// <summary>
    /// Brushes pulled from the app resources so the control follows the theme,
    /// with hard-coded fallbacks for design time / tests.
    /// </summary>
    private readonly record struct Palette(
        Brush Surface, Brush Border, Brush TextSecondary, Brush InTune, Color InTuneColor,
        Brush Flat, Brush Sharp, Brush Unstable, Brush Neutral)
    {
        public static Palette Resolve(FrameworkElement element)
        {
            Brush Get(string key, Color fallback)
            {
                if (element.TryFindResource(key) is Brush b) return b;
                var brush = new SolidColorBrush(fallback);
                brush.Freeze();
                return brush;
            }

            var inTune = Get("InTuneBrush", Color.FromRgb(0, 200, 83));
            var inTuneColor = inTune is SolidColorBrush s ? s.Color : Color.FromRgb(0, 200, 83);

            return new Palette(
                Get("SurfaceBrush", Color.FromRgb(45, 45, 45)),
                Get("BorderBrush", Color.FromRgb(61, 61, 61)),
                Get("TextSecondaryBrush", Color.FromRgb(158, 158, 158)),
                inTune, inTuneColor,
                Get("FlatBrush", Color.FromRgb(255, 152, 0)),
                Get("SharpBrush", Color.FromRgb(255, 152, 0)),
                Get("UnstableBrush", Color.FromRgb(158, 158, 158)),
                Get("NeutralBrush", Color.FromRgb(97, 97, 97)));
        }
    }
}
