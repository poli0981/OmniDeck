using OmniDeck.Core.Settings;
using OmniDeck.Core.Theming;

namespace OmniDeck.Tests.Theming;

public sealed class OdTokensTests
{
    [Theory]
    [InlineData("Dynamic", 13.5, 4, 32, 8, 180)]
    [InlineData("Standard", 15.5, 6, 36, 6, 100)]
    [InlineData("Comfort", 19, 10, 44, 6, 0)]
    public void Presets_MatchTheUiModesTable(string name, double fontBase, double density, double hitTarget, double corner, int motionMs)
    {
        var tokens = OdTokens.Compute(OdPreset.FromName(name), textScale: 1, fontScale: 1, motionEnabled: true);

        Assert.Equal(name, tokens.PresetName);
        Assert.Equal(fontBase, tokens.FontBase);
        Assert.Equal(density, tokens.Density);
        Assert.Equal(hitTarget, tokens.HitTargetMin);
        Assert.Equal(corner, tokens.CornerRadius);
        Assert.Equal(motionMs, tokens.MotionMilliseconds);
    }

    [Fact]
    public void Headings_FollowThePresetScale()
    {
        var tokens = OdTokens.Compute(OdPreset.Standard, 1, 1, motionEnabled: true);

        Assert.Equal(20.15, tokens.FontH2);
        Assert.Equal(26.2, tokens.FontH1);
        Assert.Equal(13.18, tokens.FontCaption);
    }

    [Fact]
    public void WindowsTextSize_AndTheUserScale_Multiply()
    {
        var tokens = OdTokens.Compute(OdPreset.Standard, textScale: 1.5, fontScale: 1.2, motionEnabled: true);

        Assert.Equal(27.9, tokens.FontBase);
    }

    [Theory]
    [InlineData(0.5, 13.18)]
    [InlineData(3.0, 21.7)]
    public void UserScale_IsClamped(double fontScale, double expected)
    {
        Assert.Equal(expected, OdTokens.Compute(OdPreset.Standard, 1, fontScale, motionEnabled: true).FontBase);
    }

    [Theory]
    [InlineData(0.5, 15.5)]
    [InlineData(5.0, 34.88)]
    public void TextScale_IsClampedToWhatWindowsAllows(double textScale, double expected)
    {
        Assert.Equal(expected, OdTokens.Compute(OdPreset.Standard, textScale, 1, motionEnabled: true).FontBase);
    }

    [Fact]
    public void Motion_IsOffWhenDisabled()
    {
        Assert.Equal(0, OdTokens.Compute(OdPreset.Dynamic, 1, 1, motionEnabled: false).MotionMilliseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void UnknownPresetNames_FallBackToStandard(string? name)
    {
        Assert.Same(OdPreset.Standard, OdPreset.FromName(name));
    }

    [Theory]
    [InlineData(0.2, 0.85)]
    [InlineData(1.1, 1.1)]
    [InlineData(9, 1.4)]
    [InlineData(double.NaN, 1.0)]
    public void Settings_ClampTheFontScale(double stored, double expected)
    {
        Assert.Equal(expected, new OdSettings { FontScale = stored }.Normalized().FontScale);
    }

    [Theory]
    [InlineData("#00a2ed", "#00A2ED")]
    [InlineData("00A2ED", "")]
    [InlineData("#12345G", "")]
    [InlineData("", "")]
    public void Settings_AcceptOnlyHexAccentColors(string stored, string expected)
    {
        Assert.Equal(expected, new OdSettings { AccentColor = stored }.Normalized().AccentColor);
    }
}
