using System.Windows;

namespace OmniDeck.App.Theming;

/// <summary>
/// Gives an element a preset layer of its own, so what it holds renders with that preset's tokens
/// whatever the window's preset: onboarding's preset cards are live previews (docs/UI-MODES.md §5).
/// </summary>
public static class OdPresetPreview
{
    public static readonly DependencyProperty LayerProperty = DependencyProperty.RegisterAttached(
        "Layer",
        typeof(ResourceDictionary),
        typeof(OdPresetPreview),
        new PropertyMetadata(null, OnLayerChanged));

    public static ResourceDictionary? GetLayer(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ResourceDictionary?)element.GetValue(LayerProperty);
    }

    public static void SetLayer(DependencyObject element, ResourceDictionary? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LayerProperty, value);
    }

    private static void OnLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // DynamicResource looks in an element's own resources first, so the element and everything in
        // it, stock controls' templates included, find the preview's tokens before the window's.
        if (d is FrameworkElement element)
        {
            element.Resources = e.NewValue as ResourceDictionary ?? new ResourceDictionary();
        }
    }
}
