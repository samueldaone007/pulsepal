using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace PulsePal.Controls;

/// <summary>
/// A tiny one-minute plotter with angular/stepped lines and a clipped hatched fill.
/// It redraws only when the series changes, which is intentional: a dashboard
/// should be cheap enough to leave open all day.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable<double>),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FixedMaximumProperty = DependencyProperty.Register(
        nameof(FixedMaximum),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity),
        typeof(int),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StepModeProperty = DependencyProperty.Register(
        nameof(StepMode),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGuidesProperty = DependencyProperty.Register(
        nameof(ShowGuides),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(2.6, FrameworkPropertyMetadataOptions.AffectsRender));

    private INotifyCollectionChanged? _observableValues;

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double FixedMaximum
    {
        get => (double)GetValue(FixedMaximumProperty);
        set => SetValue(FixedMaximumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public bool StepMode
    {
        get => (bool)GetValue(StepModeProperty);
        set => SetValue(StepModeProperty, value);
    }

    public bool ShowGuides
    {
        get => (bool)GetValue(ShowGuidesProperty);
        set => SetValue(ShowGuidesProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Sparkline()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = false;
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Sparkline)d;

        if (control._observableValues is not null)
        {
            control._observableValues.CollectionChanged -= control.OnCollectionChanged;
        }

        control._observableValues = e.NewValue as INotifyCollectionChanged;
        if (control._observableValues is not null)
        {
            control._observableValues.CollectionChanged += control.OnCollectionChanged;
        }

        control.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 4 || height < 4)
        {
            return;
        }

        var accent = GetAccentColor();
        var guidePen = new Pen(new SolidColorBrush(ColorFrom(accent, 15)), 1);
        guidePen.Freeze();

        if (ShowGuides)
        {
            for (var i = 1; i < 4; i++)
            {
                var y = height * i / 4;
                drawingContext.DrawLine(guidePen, new Point(0, y), new Point(width, y));
            }
        }

        var baselinePen = new Pen(new SolidColorBrush(ColorFrom(accent, 52)), 1)
        {
            DashStyle = new DashStyle([2, 4], 0)
        };
        baselinePen.Freeze();
        drawingContext.DrawLine(baselinePen, new Point(0, height - 1), new Point(width, height - 1));

        var values = Values?.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray() ?? [];
        if (values.Length < 2)
        {
            return;
        }

        var capacity = Math.Max(2, Capacity);
        if (values.Length > capacity)
        {
            values = values[^capacity..];
        }

        var maximum = FixedMaximum > 0
            ? FixedMaximum
            : Math.Max(1, values.Max() * 1.15);

        var left = 2d;
        var right = width - 2d;
        var top = 2d;
        var bottom = height - 2d;
        var plotWidth = Math.Max(1, right - left);
        var plotHeight = Math.Max(1, bottom - top);
        var offset = capacity - values.Length;

        var points = new Point[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var x = left + plotWidth * (offset + i) / (capacity - 1);
            var normalized = Math.Clamp(values[i] / maximum, 0, 1);
            var y = bottom - normalized * plotHeight;
            points[i] = new Point(x, y);
        }

        var area = BuildAreaGeometry(points, bottom, StepMode);
        area.Freeze();

        var fill = new SolidColorBrush(ColorFrom(accent, 17));
        fill.Freeze();
        drawingContext.DrawGeometry(fill, null, area);

        // Diagonal hatching, clipped to the area under the line.
        drawingContext.PushClip(area);
        var hatchPen = new Pen(new SolidColorBrush(ColorFrom(accent, 42)), 1);
        hatchPen.Freeze();
        var hatchSpacing = 9;
        for (var x = -height; x < width + height; x += hatchSpacing)
        {
            drawingContext.DrawLine(hatchPen, new Point(x, height), new Point(x + height, 0));
        }
        drawingContext.Pop();

        var line = BuildLineGeometry(points, StepMode);
        line.Freeze();
        var linePen = new Pen(new SolidColorBrush(ColorFrom(accent, 238)), StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        linePen.Freeze();
        drawingContext.DrawGeometry(null, linePen, line);

        var dotBrush = new SolidColorBrush(accent);
        dotBrush.Freeze();
        drawingContext.DrawEllipse(dotBrush, null, points[^1], 2.8, 2.8);
    }

    private static Geometry BuildLineGeometry(IReadOnlyList<Point> points, bool stepMode)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            for (var i = 1; i < points.Count; i++)
            {
                var previous = points[i - 1];
                var current = points[i];
                if (stepMode)
                {
                    ctx.LineTo(new Point(current.X, previous.Y), true, false);
                }

                ctx.LineTo(current, true, false);
            }
        }

        return geometry;
    }

    private static Geometry BuildAreaGeometry(IReadOnlyList<Point> points, double bottom, bool stepMode)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, bottom), true, true);
            ctx.LineTo(points[0], true, false);
            for (var i = 1; i < points.Count; i++)
            {
                var previous = points[i - 1];
                var current = points[i];
                if (stepMode)
                {
                    ctx.LineTo(new Point(current.X, previous.Y), true, false);
                }

                ctx.LineTo(current, true, false);
            }

            ctx.LineTo(new Point(points[^1].X, bottom), true, false);
        }

        return geometry;
    }

    private Color GetAccentColor()
    {
        return Stroke is SolidColorBrush solid ? solid.Color : Colors.Orange;
    }

    private static Color ColorFrom(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
