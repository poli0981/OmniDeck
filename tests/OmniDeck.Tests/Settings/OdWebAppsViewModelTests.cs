using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Settings;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using Serilog.Core;

namespace OmniDeck.Tests.Settings;

public sealed class OdWebAppsViewModelTests : IDisposable
{
    private static readonly OdCatalog Catalog = OdCatalogService.Load(
        Path.Combine(OdRepository.Root, "catalog", "webapps.json"), "none.json", new DateOnly(2026, 11, 2), Logger.None);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    private readonly OdSettingsService _settings;
    private readonly OdWebPreferences _preferences;
    private readonly OdAppMeta _meta;
    private readonly List<string> _cleared = [];
    private bool _confirms = true;
    private bool _clearFails;

    public OdWebAppsViewModelTests()
    {
        _settings = new OdSettingsService(new OdDataRoot(_temp.Path), Logger.None);
        _preferences = new OdWebPreferences(_settings);
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _meta = new OdAppMeta(database);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Rows_FollowTheSwitches_AndTheAnsweredPermissions() => OdSta.Run(() =>
    {
        using var host = NewHost();
        using var page = NewPage(host);
        var discord = page.Apps.Single(row => string.Equals(row.Entry.Id, "discord", StringComparison.Ordinal));
        var gmaps = page.Apps.Single(row => string.Equals(row.Entry.Id, "gmaps", StringComparison.Ordinal));

        Assert.Equal(Catalog.Apps.Count, page.Apps.Count);
        Assert.True(discord.Notifications);
        Assert.False(gmaps.Notifications);
        Assert.Equal("No permissions asked yet", discord.PermissionsText);
        Assert.False(discord.HasPermissions);

        _preferences.Remember("discord", "Microphone", allowed: true);
        _preferences.Remember("discord", "Camera", allowed: false);
        Assert.Equal("Camera blocked · Microphone allowed", discord.PermissionsText);
        Assert.True(discord.HasPermissions);

        page.ToggleNotificationsCommand.Execute(discord);
        page.ResetPermissionsCommand.Execute(discord);

        Assert.False(discord.Notifications);
        Assert.False(_preferences.NotificationsOn(discord.Entry));
        Assert.Equal("No permissions asked yet", discord.PermissionsText);
        return Task.CompletedTask;
    });

    [Fact]
    public void Profiles_GroupTheAppsThatShareASignIn_AndTheCatalogIsNamed() => OdSta.Run(() =>
    {
        using var host = NewHost();
        using var page = NewPage(host);

        var google = page.Profiles.Single(group => string.Equals(group.Profile, "google", StringComparison.Ordinal));
        Assert.Equal("YouTube, Gmail, Google Calendar, Google Drive, Google Maps", google.AppNames);
        Assert.Equal(4, page.Profiles.Count);
        Assert.Equal($"Web app catalog of {Catalog.Updated}, with {Catalog.Apps.Count} apps.", page.CatalogText);
        Assert.Equal("Not checked for a newer catalog yet.", page.CheckedText);

        var checkedAt = new DateTimeOffset(2026, 11, 2, 2, 25, 0, TimeSpan.Zero);
        _meta.Set(OdAppMetaKeys.CatalogCheckedAt, checkedAt.ToString("O", CultureInfo.InvariantCulture));
        page.Refresh();
        Assert.Equal($"Last checked for a newer catalog on {checkedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}.", page.CheckedText);
        return Task.CompletedTask;
    });

    [Fact]
    public void ClearingAProfile_AsksFirst_ThenClosesItsAppsAndClearsIt() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var page = NewPage(host);
        var google = page.Profiles.Single(group => string.Equals(group.Profile, "google", StringComparison.Ordinal));
        await host.ShowAsync("youtube");
        _meta.Set("web.cookies.google", "7");

        _confirms = false;
        await page.ClearDataCommand.ExecuteAsync(google);
        Assert.Empty(_cleared);
        Assert.Equal(OdModuleState.Active, host.Find("youtube")?.State);

        _confirms = true;
        await page.ClearDataCommand.ExecuteAsync(google);
        Assert.Equal(new[] { "google" }, _cleared, StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Closed, host.Find("youtube")?.State);
        Assert.Equal("0", _meta.Get("web.cookies.google"));
        Assert.Equal("Cleared the data of YouTube, Gmail, Google Calendar, Google Drive, Google Maps.", page.Message);

        _clearFails = true;
        await page.ClearDataCommand.ExecuteAsync(google);
        Assert.Equal("The data could not be cleared. Try again after restarting OmniDeck.", page.Message);
    });

    private OdModuleHost NewHost() => new(
        [.. Catalog.Apps.Select(entry => new OdFakeModule(entry.Id, OdRamClass.Heavy))],
        new OdDatabase(_temp.Combine("host.db"), pooling: false),
        Logger.None,
        _time,
        new OdFakeNotifier(),
        OdTestHttp.Client);

    private OdWebAppsViewModel NewPage(OdModuleHost host) => new(
        new OdCatalogService(OdTestHttp.Client, _meta, _temp.Combine("catalog.json"), Catalog, _time, Logger.None),
        _preferences,
        _settings,
        host,
        new OdSignInHealth(_meta, Logger.None),
        profile =>
        {
            if (_clearFails)
            {
                throw new InvalidOperationException("The browser is gone");
            }

            _cleared.Add(profile);
            return Task.CompletedTask;
        },
        _ => Task.FromResult(_confirms),
        Logger.None);
}
