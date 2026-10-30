using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Theming;

/// <summary>
/// Token-driven motion. Storyboards inside styles are frozen and cannot take a DynamicResource, so
/// animations run from code and read <c>Od.Motion.Duration</c> when they start: a preset change applies
/// to the next animation, and a zero duration (Comfort, the user's switch, or Windows' animation
/// effects off) jumps straight to the end state (docs/spikes/05-theme-swap.md).
/// </summary>
public static class OdMotion
{
    public static readonly DependencyProperty HoverScaleProperty = DependencyProperty.RegisterAttached(
        "HoverScale", typeof(bool), typeof(OdMotion), new PropertyMetadata(defaultValue: false, OnHoverScaleChanged));

    public static bool GetHoverScale(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(HoverScaleProperty);
    }

    public static void SetHoverScale(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(HoverScaleProperty, value);
    }

    /// <summary>The current motion duration for <paramref name="element"/>; zero means no animation.</summary>
    public static TimeSpan DurationFor(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.TryFindResource(OdTokenKeys.MotionDuration) is Duration { HasTimeSpan: true } duration
            ? duration.TimeSpan
            : TimeSpan.Zero;
    }

    private static void OnHoverScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true)
        {
            return;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new ScaleTransform(1, 1);
        element.MouseEnter += (_, _) => AnimateScale(element, 1.03);
        element.MouseLeave += (_, _) => AnimateScale(element, 1.0);
    }

    private static void AnimateScale(FrameworkElement element, double to)
    {
        if (element.RenderTransform is not ScaleTransform transform)
        {
            return;
        }

        var duration = DurationFor(element);
        if (duration == TimeSpan.Zero)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = to;
            transform.ScaleY = to;
            return;
        }

        var animation = new DoubleAnimation(to, new Duration(duration)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
