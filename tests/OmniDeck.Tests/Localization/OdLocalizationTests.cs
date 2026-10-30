using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using OmniDeck.App.Resources;
using OmniDeck.Core.Localization;

namespace OmniDeck.Tests.Localization;

[Collection(OdCultureCollection.Name)]
public sealed class OdLocalizationTests
{
    [Fact]
    public void Indexer_ReadsTheRegisteredTables_AndFallsBackToTheKey()
    {
        OdLocalization.Instance.Register(OdAppStrings.Resources);

        Assert.Equal("OmniDeck", OdLocalization.Instance["App_Name"]);
        Assert.Equal("Library", OdLocalization.Get("Shell_Library"));
        Assert.Equal("No_Such_Key", OdLocalization.Get("No_Such_Key"));
        Assert.Contains("boom", OdLocalization.Format("Error_StartFailed", "boom"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("not a culture", null)]
    [InlineData("vi", "vi")]
    [InlineData(" ja ", "ja")]
    public void ResolveCulture_FollowsWindowsForEmptyOrUnknownNames(string setting, string? expected)
    {
        var culture = OdLocalization.Instance.ResolveCulture(setting);

        Assert.Equal(expected ?? OdLocalization.Instance.SystemCulture.Name, culture.Name);
    }

    [Fact]
    public void SetCulture_RefreshesBindings_OnlyWhenTheLanguageChanges()
    {
        var localization = OdLocalization.Instance;
        var original = localization.Culture;
        var properties = new List<string?>();
        var cultureChanges = 0;
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => properties.Add(e.PropertyName);
        void OnCultureChanged(object? sender, EventArgs e)
        {
            cultureChanges++;
        }

        localization.PropertyChanged += OnPropertyChanged;
        localization.CultureChanged += OnCultureChanged;
        try
        {
            var other = string.Equals(original.Name, "ja", StringComparison.Ordinal) ? CultureInfo.GetCultureInfo("vi") : CultureInfo.GetCultureInfo("ja");
            localization.SetCulture(other);
            localization.SetCulture(other);

            Assert.Equal(new[] { "Item[]", "Culture" }, properties, StringComparer.Ordinal);
            Assert.Equal(1, cultureChanges);
            Assert.Equal(other, CultureInfo.CurrentUICulture);

            // Only English ships in P1: other languages fall back to the neutral strings.
            Assert.Equal("Library", localization["Shell_Library"]);
        }
        finally
        {
            localization.PropertyChanged -= OnPropertyChanged;
            localization.CultureChanged -= OnCultureChanged;
            localization.SetCulture(original);
        }
    }

    [Fact]
    public void LocExtension_BindsToTheIndexer()
    {
        var binding = Assert.IsType<Binding>(new OdLocExtension("Shell_Library").ProvideValue(null!));

        Assert.Equal("[Shell_Library]", binding.Path.Path);
        Assert.Same(OdLocalization.Instance, binding.Source);
        Assert.Equal(BindingMode.OneWay, binding.Mode);
        Assert.Equal("", new OdLocExtension().Key);
    }

    [Fact]
    public void TrackWindowLanguage_WorksWithoutAnApplication()
    {
        OdLocalization.Instance.TrackWindowLanguage();

        Assert.NotNull(OdLocalization.Instance.Culture);
    }
}
