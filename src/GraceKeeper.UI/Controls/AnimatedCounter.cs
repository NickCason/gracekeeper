using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GraceKeeper.UI.Controls;

public class AnimatedCounter : Control
{
    static AnimatedCounter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AnimatedCounter),
            new FrameworkPropertyMetadata(typeof(AnimatedCounter)));
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(long), typeof(AnimatedCounter),
            new PropertyMetadata(0L, OnValueChanged));

    public static readonly DependencyProperty DisplayValueProperty =
        DependencyProperty.Register(
            nameof(DisplayValue), typeof(double), typeof(AnimatedCounter),
            new PropertyMetadata(0.0, OnDisplayValueChanged));

    public static readonly DependencyProperty FormattedTextProperty =
        DependencyProperty.Register(
            nameof(FormattedText), typeof(string), typeof(AnimatedCounter),
            new PropertyMetadata("0"));

    public static readonly DependencyProperty RollDurationProperty =
        DependencyProperty.Register(
            nameof(RollDuration), typeof(Duration), typeof(AnimatedCounter),
            new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(1200))));

    public static readonly DependencyProperty EasingProperty =
        DependencyProperty.Register(
            nameof(Easing), typeof(IEasingFunction), typeof(AnimatedCounter),
            new PropertyMetadata(new QuinticEase { EasingMode = EasingMode.EaseOut }));

    public long Value
    {
        get => (long)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double DisplayValue
    {
        get => (double)GetValue(DisplayValueProperty);
        private set => SetValue(DisplayValueProperty, value);
    }

    public string FormattedText
    {
        get => (string)GetValue(FormattedTextProperty);
        private set => SetValue(FormattedTextProperty, value);
    }

    public Duration RollDuration
    {
        get => (Duration)GetValue(RollDurationProperty);
        set => SetValue(RollDurationProperty, value);
    }

    public IEasingFunction Easing
    {
        get => (IEasingFunction)GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (AnimatedCounter)d;
        var target = (long)e.NewValue;
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = c.RollDuration,
            EasingFunction = c.Easing,
            FillBehavior = FillBehavior.HoldEnd,
        };
        c.BeginAnimation(DisplayValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnDisplayValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (AnimatedCounter)d;
        var v = (long)(double)e.NewValue;
        c.FormattedText = v.ToString("N0", CultureInfo.CurrentCulture);
    }
}
