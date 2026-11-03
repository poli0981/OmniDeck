using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OmniDeck.App.Settings;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Weather;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Localization;

/// <summary>
/// What every translation must keep from the English strings (docs/TRANSLATING.md): only keys English
/// has, every placeholder English has, and no empty text. A key a translation lacks falls back to English.
/// </summary>
[Collection(OdCultureCollection.Name)]
public sealed partial class OdTranslationTests
{
    private static readonly string[] Tables = ["OmniDeck.App", "OmniDeck.Modules", "OmniDeck.Web"];

    [Fact]
    public void EveryTranslation_KeepsTheEnglishKeysAndPlaceholders()
    {
        foreach (var project in Tables)
        {
            var folder = Path.Combine(OdRepository.Root, "src", project, "Resources");
            var english = Read(Path.Combine(folder, "Strings.resx"));
            Assert.NotEmpty(english);
            foreach (var translation in Directory.EnumerateFiles(folder, "Strings.*.resx"))
            {
                var name = Path.GetFileName(translation);
                Assert.All(Read(translation), entry =>
                {
                    Assert.True(english.ContainsKey(entry.Key), $"{name}: {entry.Key} is not an English key");
                    Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{name}: {entry.Key} is empty");
                    Assert.True(Placeholders(english[entry.Key]).SetEquals(Placeholders(entry.Value)), $"{name}: {entry.Key} has other placeholders than English");
                });
            }
        }
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("vi", "Tiếng Việt")]
    [InlineData("ja", "日本語")]
    public void Languages_AreNamedInThemselves(string name, string expected)
    {
        Assert.Equal(expected, OdLanguages.NativeName(name));
    }

    [Fact]
    public void Options_ReadTheirLabelsAgain_InPlace()
    {
        var calls = 0;
        var option = new OdWeatherUnitsOption(OdWeatherUnits.Metric, () => "label " + ++calls);
        var choice = new OdChoice<int>(1, () => "title " + ++calls);
        var changed = new List<string?>();
        option.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        ((INotifyPropertyChanged)choice).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        option.RefreshLabel();
        choice.RefreshTitle();

        Assert.Equal("label 3", option.Label);
        Assert.Equal("title 4", choice.ToString());
        Assert.Equal(new[] { "Label", "Title" }, changed, StringComparer.Ordinal);
    }

    [Fact]
    public void AChangeOfLanguage_RefreshesTheSettingsLists_WithoutReplacingThem() => OdSta.Run(() =>
    {
        using var temp = new OdTempDirectory();
        var settings = new OdSettingsService(new OdDataRoot(temp.Path), Logger.None);
        var database = new OdDatabase(temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        var onboarding = new OdOnboardingViewModel(settings, new OdAppMeta(database));
        var language = onboarding.Languages[0];
        var preset = onboarding.Presets[0];
        var changed = new List<string?>();
        ((INotifyPropertyChanged)language).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        preset.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var original = OdLocalization.Instance.Culture;
        try
        {
            OdLocalization.Instance.SetCulture(CultureInfo.GetCultureInfo("fr"));
        }
        finally
        {
            OdLocalization.Instance.SetCulture(original);
        }

        Assert.Same(language, onboarding.Languages[0]);
        Assert.Contains("Title", changed, StringComparer.Ordinal);
        Assert.Contains("Hint", changed, StringComparer.Ordinal);
        GC.KeepAlive(onboarding);
        return Task.CompletedTask;
    });

    private static Dictionary<string, string> Read(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "", StringComparer.Ordinal);

    private static HashSet<int> Placeholders(string text) =>
        [.. Placeholder().Matches(text).Select(match => int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture))];

    [GeneratedRegex(@"\{(?<index>\d+)(?:[,:][^}]*)?\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Placeholder();
}
