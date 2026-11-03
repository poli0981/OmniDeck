using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Updates;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Governor;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Tests.Updates;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using Serilog.Core;

namespace OmniDeck.Tests.Settings;

public sealed class OdSettingsPagesTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly OdSettingsService _settings;
    private readonly OdFakeStartup _startup = new();
    private readonly OdFakeUpdates _updates = new();
    private readonly List<string> _openedFolders = [];
    private readonly List<Uri> _openedLinks = [];

    public OdSettingsPagesTests()
    {
        _settings = new OdSettingsService(new OdDataRoot(_temp.Path), Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Settings_ShowTheStoredValues_AndSaveEveryChange()
    {
        using var page = NewSettingsPage();
        Assert.Equal("Standard", page.Preset);
        Assert.True(page.UseSystemAccent);
        Assert.Equal(5, page.IdleAfterMinutes);

        page.Language = "en";
        page.CloseToTray = false;
        page.Theme = OdThemeMode.Dark;
        page.Preset = "Comfort";
        page.FontScale = 1.2;
        page.MotionEnabled = false;
        page.IdleAfterMinutes = 7;
        page.HibernateAfterMinutes = 45;
        page.SoftCapMegabytes = 2048;
        page.HardCapMegabytes = null;

        var saved = _settings.Current;
        Assert.Equal("en", saved.Language);
        Assert.False(saved.CloseToTray);
        Assert.Equal(OdThemeMode.Dark, saved.Theme);
        Assert.Equal("Comfort", saved.Preset);
        Assert.Equal(1.2, saved.FontScale);
        Assert.False(saved.MotionEnabled);
        Assert.Equal(7, saved.Memory.IdleAfterMinutes);
        Assert.Equal(45, saved.Memory.HibernateAfterMinutes);
        Assert.Equal(2048, saved.Memory.SoftCapMegabytes);
        Assert.Equal(0, saved.Memory.HardCapMegabytes);
    }

    [Fact]
    public void PreReleases_FollowThisBuild_UntilTheUserChooses()
    {
        using var page = NewSettingsPage();

        // This build is a pre-release (0.x), so without a choice it takes pre-releases.
        Assert.Null(_settings.Current.Prereleases);
        Assert.True(page.ReceivePrereleases);

        page.ReceivePrereleases = false;
        Assert.False(_settings.Current.Prereleases);

        page.ReceivePrereleases = true;
        Assert.True(_settings.Current.Prereleases);
    }

    [Fact]
    public void Accent_SwitchesBetweenWindowsAndASwatch()
    {
        using var page = NewSettingsPage();

        Assert.Null(page.SelectedSwatch);
        page.UseSystemAccent = false;
        Assert.Equal(page.AccentSwatches[0].Color, _settings.Current.AccentColor);
        Assert.Equal("Blue", page.SelectedSwatch?.ToString());
        page.SelectedSwatch = page.AccentSwatches[5];
        Assert.Equal("#10893E", _settings.Current.AccentColor);
        Assert.Equal("Green", page.SelectedSwatch?.Name);
        page.UseSystemAccent = true;

        Assert.Equal("", _settings.Current.AccentColor);
        Assert.Null(page.SelectedSwatch);
    }

    [Fact]
    public void StartWithWindows_FollowsTheRegistration()
    {
        _startup.Set(true);
        using var page = NewSettingsPage();
        Assert.True(page.StartWithWindows);

        page.StartWithWindows = false;

        Assert.False(_startup.IsEnabled);
        Assert.False(_settings.Current.StartWithWindows);
    }

    [Fact]
    public void ChangesMadeElsewhere_ShowUpWithoutBeingWrittenBack()
    {
        using var page = NewSettingsPage();
        var writes = 0;
        _settings.Changed += (_, _) => writes++;

        _settings.Update(s => s with { Preset = "Dynamic", FontScale = 1.1 });

        Assert.Equal("Dynamic", page.Preset);
        Assert.Equal(1.1, page.FontScale);
        Assert.Equal(1, writes);
    }

    [Fact]
    public void Data_ExportsImportsAndOpensTheFolder()
    {
        using var page = NewSettingsPage();
        var file = _temp.Combine("export.json");
        page.Preset = "Comfort";

        page.ExportTo(file);
        Assert.Equal("Settings exported.", page.DataMessage);
        page.Preset = "Dynamic";
        page.ImportFrom(file);
        Assert.Equal("Settings imported.", page.DataMessage);
        Assert.Equal("Comfort", page.Preset);

        File.WriteAllText(_temp.Combine("broken.json"), "{ nope");
        page.ImportFrom(_temp.Combine("broken.json"));
        Assert.Equal("That file is not an OmniDeck settings file.", page.DataMessage);
        Assert.True(File.Exists(_temp.Combine("broken.json")));

        page.OpenDataFolderCommand.Execute(null);
        Assert.Equal(new[] { _temp.Path }, _openedFolders, StringComparer.Ordinal);
        Assert.Equal(_temp.Path, page.DataFolder);
    }

    [Fact]
    public void Sections_LanguagesThemesAndPresets()
    {
        using var page = NewSettingsPage();

        page.ShowSection(OdSettingsSection.Memory);

        Assert.Equal(2, page.SectionIndex);
        Assert.Equal(new[] { "", "en", "vi" }, page.Languages.Select(l => l.Value), StringComparer.Ordinal);
        Assert.Equal("Tiếng Việt", page.Languages[2].Title);
        Assert.Equal(new[] { "Follow Windows", "Light", "Dark" }, page.Themes.Select(t => t.Title), StringComparer.Ordinal);
        Assert.Equal(new[] { "Dynamic", "Standard", "Comfort" }, page.Presets.Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal("Larger text and targets, no motion.", page.Presets[2].Hint);
        Assert.Equal("Comfort", page.Presets[2].ToString());
        Assert.Null(page.Presets[2].Preview);
        Assert.Equal("Dark", page.Themes[2].ToString());
        Assert.NotNull(page.Memory);
    }

    [Fact]
    public void Onboarding_AppliesAsYouChoose_AndRecordsThatItIsDone()
    {
        var meta = NewMeta();
        Assert.True(OdOnboardingViewModel.IsNeeded(meta));
        var onboarding = new OdOnboardingViewModel(_settings, meta, preset => new System.Windows.ResourceDictionary { ["preset"] = preset.Name });
        var finished = 0;
        onboarding.Finished += (_, _) => finished++;

        onboarding.Preset = "Comfort";
        onboarding.Language = "en";
        onboarding.FinishCommand.Execute(null);

        Assert.Equal("Comfort", _settings.Current.Preset);
        Assert.Equal("en", _settings.Current.Language);
        Assert.False(OdOnboardingViewModel.IsNeeded(meta));
        Assert.Equal(1, finished);
        Assert.Equal(3, onboarding.Presets.Count);
        Assert.Equal("Comfort", onboarding.Presets[2].Preview!["preset"]);
        Assert.Equal(3, onboarding.Languages.Count);
    }

    [Fact]
    public void About_WalksThroughTheUpdateStates() => OdSta.Run(async () =>
    {
        var about = new OdAboutViewModel(_updates, _openedLinks.Add);
        Assert.StartsWith("Version ", about.VersionText, StringComparison.Ordinal);

        _updates.Installed = false;
        await about.CheckCommand.ExecuteAsync(null);
        Assert.Equal(OdUpdateState.NotInstalled, about.State);

        _updates.Installed = true;
        await about.CheckCommand.ExecuteAsync(null);
        Assert.Equal(OdUpdateState.UpToDate, about.State);
        Assert.Equal("OmniDeck is up to date.", about.StatusText);

        _updates.Available = "0.1.0-alpha.2";
        await about.CheckCommand.ExecuteAsync(null);
        Assert.Equal(OdUpdateState.Available, about.State);
        Assert.True(about.CanDownload);
        Assert.Equal("Version 0.1.0-alpha.2 is available.", about.StatusText);

        await about.DownloadCommand.ExecuteAsync(null);
        Assert.True(_updates.Applied);

        _updates.Failure = new HttpRequestException("offline");
        await about.CheckCommand.ExecuteAsync(null);
        Assert.Equal(OdUpdateState.Failed, about.State);
        Assert.Equal("Could not update: offline", about.StatusText);
        await about.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(OdUpdateState.Failed, about.State);
    });

    [Fact]
    public void OpeningAbout_ChecksForUpdatesOnce()
    {
        using var page = NewSettingsPage();

        page.ShowSection(OdSettingsSection.About);
        page.ShowSection(OdSettingsSection.General);
        page.ShowSection(OdSettingsSection.About);

        Assert.Equal(1, _updates.Checks);
        Assert.Equal(OdUpdateState.UpToDate, page.About.State);
    }

    [Fact]
    public void About_OpensLinksAndTheLegalDocuments()
    {
        var about = new OdAboutViewModel(_updates, _openedLinks.Add);
        var legal = 0;
        var reports = 0;
        about.LegalRequested += (_, _) => legal++;
        about.BugReportRequested += (_, _) => reports++;

        about.OpenLinkCommand.Execute(OdAboutViewModel.Discussions);
        about.ShowLegalCommand.Execute(null);
        about.ReportBugCommand.Execute(null);

        Assert.Equal(new[] { new Uri("https://github.com/poli0981/OmniDeck/discussions") }, _openedLinks);
        Assert.Equal(1, legal);
        Assert.Equal(1, reports);
    }

    [Fact]
    public void RunKey_RegistersAMinimizedStart()
    {
        var keyPath = @"Software\OmniDeck.Tests\" + Guid.NewGuid().ToString("N");
        var startup = new OdRunKeyStartup(keyPath, "OmniDeck", @"C:\Apps\OmniDeck.exe");
        try
        {
            Assert.False(startup.IsEnabled);
            startup.Set(true);
            Assert.True(startup.IsEnabled);
            Assert.Equal("\"C:\\Apps\\OmniDeck.exe\" --minimized", startup.Command);
            startup.Set(false);
            startup.Set(false);
            Assert.False(startup.IsEnabled);
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }

        Assert.EndsWith(".exe\" --minimized", new OdRunKeyStartup().Command, StringComparison.OrdinalIgnoreCase);
    }

    private OdAppMeta NewMeta()
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        return new OdAppMeta(database);
    }

    private OdSettingsViewModel NewSettingsPage()
    {
        var host = new OdModuleHost([], new OdDatabase(_temp.Combine("omnideck.db"), pooling: false), Logger.None, _time, new OdFakeNotifier(), OdTestHttp.Client);
        var governor = new OdRamGovernor(host, _settings, new OdFakeMemory(), [], _time, new OdFakeNotifier(), Logger.None);
        var memory = new OdMemoryViewModel(host, governor, _settings, _time);
        var meta = NewMeta();
        var webApps = new OdWebAppsViewModel(
            new OdCatalogService(OdTestHttp.Client, meta, _temp.Combine("catalog.json"), OdCatalog.Empty, _time, Logger.None),
            new OdWebPreferences(_settings),
            _settings,
            host,
            new OdSignInHealth(meta, Logger.None),
            _ => Task.CompletedTask,
            _ => Task.FromResult(false),
            Logger.None);
        return new OdSettingsViewModel(_settings, _startup, new OdDataRoot(_temp.Path), memory, webApps, new OdAboutViewModel(_updates, _openedLinks.Add), _openedFolders.Add);
    }

    private sealed class OdFakeStartup : IOdStartupRegistration
    {
        public bool IsEnabled { get; private set; }

        public void Set(bool enabled) => IsEnabled = enabled;
    }
}
