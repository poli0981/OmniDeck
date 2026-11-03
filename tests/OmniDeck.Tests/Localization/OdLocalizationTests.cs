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
            var other = string.Equals(original.Name, "fr", StringComparison.Ordinal) ? CultureInfo.GetCultureInfo("de") : CultureInfo.GetCultureInfo("fr");
            localization.SetCulture(other);
            localization.SetCulture(other);

            Assert.Equal(new[] { "Item[]", "Culture" }, properties, StringComparer.Ordinal);
            Assert.Equal(1, cultureChanges);
            Assert.Equal(other, CultureInfo.CurrentUICulture);

            // A language OmniDeck has no strings for falls back to the neutral (English) ones.
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
    public void FollowCulture_CallsEveryFollower_EvenWhenOneFails()
    {
        var localization = OdLocalization.Instance;
        var original = localization.Culture;
        var failing = new Follower(fails: true);
        var following = new Follower();
        var gone = new Follower();
        OdLocalization.FollowCulture(failing.OnCultureChanged);
        OdLocalization.FollowCulture(following.OnCultureChanged);
        OdLocalization.FollowCulture(gone.OnCultureChanged);
        OdLocalization.UnfollowCulture(gone.OnCultureChanged);
        try
        {
            localization.SetCulture(string.Equals(original.Name, "fr", StringComparison.Ordinal) ? CultureInfo.GetCultureInfo("de") : CultureInfo.GetCultureInfo("fr"));

            Assert.Equal(1, failing.Calls);
            Assert.Equal(1, following.Calls);
            Assert.Equal(0, gone.Calls);
        }
        finally
        {
            OdLocalization.UnfollowCulture(failing.OnCultureChanged);
            OdLocalization.UnfollowCulture(following.OnCultureChanged);
            localization.SetCulture(original);
        }

        Assert.Equal(1, following.Calls);
    }

    [Fact]
    public void FollowCulture_TakesOnlyAMethodOfAnObject() =>
        Assert.Throws<ArgumentException>(() => OdLocalization.FollowCulture(IgnoreTheChange));

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

    private static void IgnoreTheChange(object? sender, EventArgs e)
    {
        // A static method has no object for FollowCulture to hold weakly.
    }

    private sealed class Follower(bool fails = false)
    {
        public int Calls { get; private set; }

        public void OnCultureChanged(object? sender, EventArgs e)
        {
            Calls++;
            if (fails)
            {
                throw new InvalidOperationException("This follower's data is gone.");
            }
        }
    }
}
