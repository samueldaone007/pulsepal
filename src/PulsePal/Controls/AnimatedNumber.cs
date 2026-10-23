using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PulsePal.Controls;

/// <summary>
/// A TextBlock that eases toward a new value instead of snapping.
/// Non-finite values fall back to "n/a" without pretending they are zero.
/// </summary>
public sealed class AnimatedNumber : TextBlock
{
    private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue),
        typeof(double),
        typeof(AnimatedNumber),
        new PropertyMetadata(0d, OnDisplayValueChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(AnimatedNumber),
        new PropertyMetadata(double.NaN, OnValueChanged));

    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(
        nameof(ValueFormat),
        typeof(string),
        typeof(AnimatedNumber),
        new PropertyMetadata("0", OnFormatChanged));

    public static readonly DependencyProperty FallbackTextProperty = DependencyProperty.Register(
        nameof(FallbackText),
        typeof(string),
        typeof(AnimatedNumber),
        new PropertyMetadata("n/a"));

    public static readonly DependencyProperty AnimationMillisecondsProperty = DependencyProperty.Register(
        nameof(AnimationMilliseconds),
        typeof(int),
        typeof(AnimatedNumber),
        new PropertyMetadata(420));

    private double DisplayValue
    {
        get => (double)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public string FallbackText
    {
        get => (string)GetValue(FallbackTextProperty);
        set => SetValue(FallbackTextProperty, value);
    }

    public int AnimationMilliseconds
    {
        get => (int)GetValue(AnimationMillisecondsProperty);
        set => SetValue(AnimationMillisecondsProperty, value);
    }

    public AnimatedNumber()
    {
        Text = FallbackText;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AnimatedNumber)d;
        var target = (double)e.NewValue;

        if (double.IsNaN(target) || double.IsInfinity(target))
        {
            control.BeginAnimation(DisplayValueProperty, null);
            control.Text = control.FallbackText;
            return;
        }

        var from = control.DisplayValue;
        if (double.IsNaN(from) || double.IsInfinity(from))
        {
            from = 0;
        }

        if (Math.Abs(target - from) < 0.001)
        {
            control.BeginAnimation(DisplayValueProperty, null);
            control.DisplayValue = target;
            var format = string.IsNullOrWhiteSpace(control.ValueFormat) ? "0" : control.ValueFormat;
            control.Text = target.ToString(format, CultureInfo.CurrentCulture);
            return;
        }

        var animation = new DoubleAnimation
        {
            From = from,
            To = target,
            Duration = TimeSpan.FromMilliseconds(Math.Clamp(control.AnimationMilliseconds, 0, 2000)),
            EasingFunction = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        control.BeginAnimation(DisplayValueProperty, animation);
    }

    private static void OnDisplayValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AnimatedNumber)d;
        var value = Math.Max(0, (double)e.NewValue);
        var format = string.IsNullOrWhiteSpace(control.ValueFormat) ? "0" : control.ValueFormat;
        control.Text = value.ToString(format, CultureInfo.CurrentCulture);
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AnimatedNumber)d;
        if (!double.IsNaN(control.DisplayValue) && !double.IsInfinity(control.DisplayValue))
        {
            var format = string.IsNullOrWhiteSpace(control.ValueFormat) ? "0" : control.ValueFormat;
            control.Text = control.DisplayValue.ToString(format, CultureInfo.CurrentCulture);
        }
    }
}
