using System.Windows;
using OmniDeck.App.Theming;
using OmniDeck.Core.Theming;

namespace OmniDeck.Tests.Theming;

public sealed class OdThemeResourcesTests
{
    [Fact]
    public void Build_PublishesEveryTokenWithItsWpfType()
    {
        var resources = OdThemeResources.Build(OdTokens.Compute(OdPreset.Comfort, 1, 1, motionEnabled: true));

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
    }

    [Fact]
    public void Apply_ReplacesThePresetLayerInPlace()
    {
        var app = new ResourceDictionary();
        app.MergedDictionaries.Add(new ResourceDictionary { ["wpfui"] = 1 });

        OdThemeResources.Apply(app, OdThemeResources.Build(OdTokens.Compute(OdPreset.Standard, 1, 1, true)));
        OdThemeResources.Apply(app, OdThemeResources.Build(OdTokens.Compute(OdPreset.Dynamic, 1, 1, true)));

        Assert.Equal(2, app.MergedDictionaries.Count);
        Assert.Equal(1, app.MergedDictionaries[0]["wpfui"]);
        Assert.Equal("Dynamic", app.MergedDictionaries[1][OdTokenKeys.PresetMarker]);
    }

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
