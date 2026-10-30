using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapper;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Modules;

public sealed class OdModuleSettingsStoreTests : IDisposable
{
    private static readonly JsonTypeInfo<string[]> Zones = (JsonTypeInfo<string[]>)JsonSerializerOptions.Default.GetTypeInfo(typeof(string[]));

    private readonly OdTempDirectory _temp = new();
    private readonly OdDatabase _database;

    public OdModuleSettingsStoreTests()
    {
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void NothingStored_GivesNull()
    {
        Assert.Null(new OdModuleSettingsStore(_database, "clock").Get(Zones));
    }

    [Fact]
    public void Set_RoundTrips_AndOverwrites()
    {
        var store = new OdModuleSettingsStore(_database, "clock");

        store.Set(["Europe/Paris"], Zones);
        store.Set(["Asia/Tokyo", "UTC"], Zones);

        Assert.Equal(new[] { "Asia/Tokyo", "UTC" }, new OdModuleSettingsStore(_database, "clock").Get(Zones));
        Assert.Null(new OdModuleSettingsStore(_database, "timer").Get(Zones));
    }

    [Fact]
    public void ADocumentThatNoLongerParses_GivesNull()
    {
        using (var connection = _database.Open())
        {
            connection.Execute("INSERT INTO module_settings (module_id, json) VALUES ('clock', '{ broken')");
        }

        Assert.Null(new OdModuleSettingsStore(_database, "clock").Get(Zones));
    }
}
