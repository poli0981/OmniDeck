using System.IO.Packaging;
using System.Windows;
using System.Windows.Media;
using OmniDeck.Core.Theming;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Theming;

public sealed class OdPaletteTests
{
    public static TheoryData<string, OdThemeVariant> Palettes => new()
    {
        { "Dynamic", OdThemeVariant.Light },
        { "Standard", OdThemeVariant.Light },
        { "Comfort", OdThemeVariant.Light },
        { "Dynamic", OdThemeVariant.Dark },
        { "Standard", OdThemeVariant.Dark },
        { "Comfort", OdThemeVariant.Dark },
    };

    [Fact]
    public void Contrast_FollowsWcag()
    {
        var white = new OdRgb(255, 255, 255);
        var black = new OdRgb(0, 0, 0);

        Assert.Equal(21, OdContrast.Ratio(black, white), 3);
        Assert.Equal(1, OdContrast.Ratio(white, white), 3);
        Assert.Equal(4.54, OdContrast.Ratio(new OdRgb(0x76, 0x76, 0x76), white), 2);
        Assert.Equal(new OdRgb(127, 127, 127), OdContrast.Over(0x80000000, white));
        Assert.Equal(new OdRgb(0x12, 0x34, 0x56), OdContrast.Over(0xFF123456, white));
    }

    [Fact]
    public void WpfUisOwnTertiaryText_OnTheLightTheme_FallsShortOfAa() => OdSta.Run(() =>
    {
        var light = WpfUiTheme(OdThemeVariant.Light);

        Assert.InRange(OdContrast.Ratio(Argb(light, "TextFillColorTertiary"), Rgb(light, "SolidBackgroundFillColorBase")), 3, OdContrast.Aa);
        return Task.CompletedTask;
    });

    [Fact]
    public void ThePresetLayer_ReplacesTextBrushesWpfUiDefines() => OdSta.Run(() =>
    {
        // OdThemeResources puts its own brushes under these keys; a WPF-UI update that renamed them
        // would leave the palettes unused.
        foreach (var variant in new[] { OdThemeVariant.Light, OdThemeVariant.Dark })
        {
            var theme = WpfUiTheme(variant);
            Assert.IsType<SolidColorBrush>(theme["TextFillColorSecondaryBrush"]);
            Assert.IsType<SolidColorBrush>(theme["TextFillColorTertiaryBrush"]);
        }

        return Task.CompletedTask;
    });

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryPresetsText_MeetsItsTarget_OnEveryWpfUiSurface(string preset, OdThemeVariant variant) => OdSta.Run(() =>
    {
        // Read from WPF-UI's own theme dictionaries, so that an update of WPF-UI is checked too.
        var theme = WpfUiTheme(variant);
        var target = OdPreset.FromName(preset).ContrastTarget;
        var palette = OdPalette.For(OdPreset.FromName(preset), variant)!;

        Assert.All(Surfaces(theme), surface =>
        {
            Assert.True(OdContrast.Ratio(palette.TextSecondary, surface) >= target, $"secondary on {surface}");
            Assert.True(OdContrast.Ratio(palette.TextTertiary, surface) >= target, $"tertiary on {surface}");
        });
        return Task.CompletedTask;
    });

    [Fact]
    public void Comfort_AimsForAaa_AndHighContrastHasNoPaletteOfOurs()
    {
        Assert.Equal(OdContrast.Aaa, OdPreset.Comfort.ContrastTarget);
        Assert.Equal(OdContrast.Aa, OdPreset.Standard.ContrastTarget);
        Assert.Null(OdPalette.For(OdPreset.Comfort, OdThemeVariant.HighContrast));
    }

    /// <summary>The surfaces text sits on: the solid backgrounds, and cards, layers and controls over the base.</summary>
    private static List<OdRgb> Surfaces(ResourceDictionary theme)
    {
        var solidBase = Rgb(theme, "SolidBackgroundFillColorBase");
        return
        [
            solidBase,
            Rgb(theme, "SolidBackgroundFillColorSecondary"),
            Rgb(theme, "ApplicationBackgroundColor"),
            .. new[] { "CardBackgroundFillColorDefault", "LayerFillColorDefault", "LayerOnMicaBaseAltFillColorDefault", "ControlFillColorDefault" }
                .Select(layer => OdContrast.Over(Argb(theme, layer), solidBase)),
        ];
    }

    private static ResourceDictionary WpfUiTheme(OdThemeVariant variant)
    {
        // Registers the pack:// scheme, which an Application otherwise does.
        _ = PackUriHelper.UriSchemePack;
        return (ResourceDictionary)Application.LoadComponent(new Uri($"/Wpf.Ui;component/Resources/Theme/{variant}.xaml", UriKind.Relative));
    }

    private static uint Argb(ResourceDictionary theme, string key)
    {
        var color = (Color)theme[key];
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }

    private static OdRgb Rgb(ResourceDictionary theme, string key) => OdRgb.FromArgb(Argb(theme, key));
}
