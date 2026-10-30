using System.Windows.Media.Animation;

namespace ThemeLab;

/// <summary>
/// Code-driven motion: storyboards inside styles are frozen and cannot take DynamicResource, so the
/// animation reads Od.Motion.Duration when it starts. A preset swap applies to the very next hover,
/// and the OS "animation effects" switch (SystemParameters.ClientAreaAnimation) forces motion off.
/// </summary>
public static class OdMotion
{
    public static readonly DependencyProperty HoverScaleProperty = DependencyProperty.RegisterAttached(
        "HoverScale", typeof(bool), typeof(OdMotion), new PropertyMetadata(false, OnHoverScaleChanged));

    public static bool GetHoverScale(DependencyObject element) => (bool)element.GetValue(HoverScaleProperty);

    public static void SetHoverScale(DependencyObject element, bool value) => element.SetValue(HoverScaleProperty, value);

    private static void OnHoverScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true)
        {
            return;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new ScaleTransform(1, 1);
        element.MouseEnter += (_, _) => Animate(element, 1.04);
        element.MouseLeave += (_, _) => Animate(element, 1.0);
    }

    private static void Animate(FrameworkElement element, double to)
    {
        var transform = (ScaleTransform)element.RenderTransform;
        var duration = element.TryFindResource("Od.Motion.Duration") is Duration d ? d : new Duration(TimeSpan.Zero);
        if (!SystemParameters.ClientAreaAnimation || duration.TimeSpan == TimeSpan.Zero)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = to;
            transform.ScaleY = to;
            return;
        }

        var animation = new DoubleAnimation(to, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
