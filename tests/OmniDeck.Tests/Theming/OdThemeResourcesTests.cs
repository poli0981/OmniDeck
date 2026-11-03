using System.Windows;
using System.Windows.Controls;
using OmniDeck.App.Theming;
using OmniDeck.Core.Theming;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Theming;

public sealed class OdThemeResourcesTests
{
    [Fact]
    public void Build_PublishesEveryTokenWithItsWpfType()
    {
        var resources = OdThemeResources.Build(OdTokens.Compute(OdPreset.Comfort, 1, 1, motionEnabled: true), OdThemeVariant.Light);

        Assert.Equal("Comfort", resources[OdTokenKeys.PresetMarker]);
        Assert.Equal(19.0, resources[OdTokenKeys.FontBase]);
        Assert.Equal(new Thickness(20, 10, 20, 10), resources[OdTokenKeys.PaddingControl]);
        Assert.Equal(new Thickness(30), resources[OdTokenKeys.PaddingCard]);
        Assert.Equal(new Thickness(0, 0, 20, 20), resources[OdTokenKeys.MarginTile]);
        Assert.Equal(new CornerRadius(6), resources[OdTokenKeys.CornerRadius]);
        Assert.Equal(new Duration(TimeSpan.Zero), resources[OdTokenKeys.MotionDuration]);
        Assert.Equal(6, resources[OdTokenKeys.NavMaxVisibleItems]);
        Assert.Equal(new CornerRadius(6), resources["ControlCornerRadius"]);
        Assert.Equal(19.0, resources["ControlContentThemeFontSize"]);
        Assert.Equal(new Thickness(20, 0, 0, 0), resources[OdTokenKeys.MarginLeading]);
        Assert.Equal(new Thickness(0, 0, 20, 0), resources[OdTokenKeys.MarginInline]);
        Assert.Equal(new Thickness(5), resources[OdTokenKeys.MarginTight]);
        Assert.Equal(22.0, resources[OdTokenKeys.NavIconSize]);
        Assert.Equal(OdContrast.Aaa, resources[OdTokenKeys.ContrastTarget]);
        Assert.Equal(0.7, resources[OdTokenKeys.DimOpacity]);
        Assert.Equal(323.0, resources[OdTokenKeys.WidthTile]);
        Assert.Equal(390.0, resources[OdTokenKeys.WidthTileWide]);
        Assert.Equal(219.0, resources[OdTokenKeys.WidthField]);
        Assert.Equal(874.0, resources[OdTokenKeys.WidthReadable]);
    }

    [Theory]
    [InlineData(OdThemeVariant.Light, 0xB0000000u)]
    [InlineData(OdThemeVariant.Dark, 0xB8FFFFFFu)]
    public void Build_CarriesThePresetsTextPalette_OnLightAndDark(OdThemeVariant variant, uint tertiary)
    {
        var resources = OdThemeResources.Build(OdTokens.Compute(OdPreset.Comfort, 1, 1, true), variant);

        var brush = Assert.IsType<System.Windows.Media.SolidColorBrush>(resources["TextFillColorTertiaryBrush"]);
        Assert.Equal(tertiary, ((uint)brush.Color.A << 24) | ((uint)brush.Color.R << 16) | ((uint)brush.Color.G << 8) | brush.Color.B);
        Assert.True(brush.IsFrozen);
        Assert.True(resources.Contains("TextFillColorSecondaryBrush"));
    }

    [Fact]
    public void Build_LeavesHighContrastToWindows()
    {
        var resources = OdThemeResources.Build(OdTokens.Compute(OdPreset.Comfort, 1, 1, true), OdThemeVariant.HighContrast);

        Assert.False(resources.Contains("TextFillColorSecondaryBrush"));
        Assert.False(resources.Contains("TextFillColorTertiaryBrush"));
        Assert.Equal(1.0, resources[OdTokenKeys.DimOpacity]);
    }

    [Fact]
    public void Apply_ReplacesThePresetLayerInPlace()
    {
        var app = new ResourceDictionary();
        app.MergedDictionaries.Add(new ResourceDictionary { ["wpfui"] = 1 });

        OdThemeResources.Apply(app, OdThemeResources.Build(OdTokens.Compute(OdPreset.Standard, 1, 1, true), OdThemeVariant.Light));
        OdThemeResources.Apply(app, OdThemeResources.Build(OdTokens.Compute(OdPreset.Dynamic, 1, 1, true), OdThemeVariant.Dark));

        Assert.Equal(2, app.MergedDictionaries.Count);
        Assert.Equal(1, app.MergedDictionaries[0]["wpfui"]);
        Assert.Equal("Dynamic", app.MergedDictionaries[1][OdTokenKeys.PresetMarker]);
    }

    [Fact]
    public void APreviewLayer_IsFoundBeforeTheWindowsTokens() => OdSta.Run(() =>
    {
        var card = new StackPanel();
        var text = new TextBlock();
        card.Children.Add(text);
        var window = new Border { Child = card, Resources = OdThemeResources.Build(OdTokens.Compute(OdPreset.Standard, 1, 1, true), OdThemeVariant.Dark) };
        text.SetResourceReference(TextBlock.FontSizeProperty, OdTokenKeys.FontBase);

        OdPresetPreview.SetLayer(card, OdThemeResources.Build(OdTokens.Compute(OdPreset.Comfort, 1, 1, true), OdThemeVariant.Dark));
        Assert.Equal(19.0, text.FontSize);
        Assert.Equal("Comfort", text.FindResource(OdTokenKeys.PresetMarker));

        OdPresetPreview.SetLayer(card, null);
        Assert.Null(OdPresetPreview.GetLayer(card));
        Assert.Equal(15.5, text.FontSize);
        Assert.Equal("Standard", window.FindResource(OdTokenKeys.PresetMarker));
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(225, 2.25)]
    [InlineData(99, 1.0)]
    [InlineData(300, 1.0)]
    [InlineData("150", 1.0)]
    [InlineData(null, 1.0)]
    public void TextScale_ReadsTheRegistryPercentage(object? value, double expected)
    {
        Assert.Equal(expected, OdTextScale.FromRegistryValue(value));
    }
}
