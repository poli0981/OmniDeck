using Dapper;
using Microsoft.Data.Sqlite;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Storage;

public sealed class OdDatabaseTests
{
    private static readonly OdMigration Notes = new(2, "notes", "CREATE TABLE notes (id INTEGER PRIMARY KEY, text TEXT NOT NULL);");

    [Fact]
    public void EmptyDatabase_IsVersionZero()
    {
        using var temp = new OdTempDirectory();

        Assert.Equal(0, NewDatabase(temp).SchemaVersion());
    }

    [Fact]
    public void CoreMigrations_StartAtOne_AndHaveNoGaps()
    {
        var versions = OdCoreMigrations.All.Select(m => m.Version).ToList();

        Assert.Equal(Enumerable.Range(1, versions.Count), versions);
        Assert.Equal("app_meta", OdCoreMigrations.All[0].Name);
    }

    [Fact]
    public void Migrate_AppliesEachStepOnce()
    {
        using var temp = new OdTempDirectory();
        var database = NewDatabase(temp);
        OdMigration[] migrations = [.. OdCoreMigrations.All, Notes];

        Assert.Equal(2, database.Migrate(migrations));
        Assert.Equal(0, database.Migrate(migrations));
        Assert.Equal(2, database.SchemaVersion());
    }

    [Fact]
    public void Migrate_AFailingStep_RollsBackAndKeepsTheVersion()
    {
        using var temp = new OdTempDirectory();
        var database = NewDatabase(temp);
        database.Migrate([.. OdCoreMigrations.All, Notes]);
        var broken = new OdMigration(3, "broken", "CREATE TABLE half (id INTEGER); INSERT INTO nowhere VALUES (1);");

        Assert.Throws<SqliteException>(() => database.Migrate([.. OdCoreMigrations.All, Notes, broken]));

        Assert.Equal(2, database.SchemaVersion());
        using var connection = database.Open();
        Assert.Equal(0, connection.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'half'"));
    }

    [Fact]
    public void Migrate_RejectsDuplicateVersions()
    {
        using var temp = new OdTempDirectory();

        Assert.Throws<ArgumentException>(() => NewDatabase(temp).Migrate([.. OdCoreMigrations.All, Notes, Notes with { Name = "again" }]));
    }

    [Fact]
    public void AppMeta_SetsAndOverwritesValues()
    {
        using var temp = new OdTempDirectory();
        var database = NewDatabase(temp);
        database.Migrate(OdCoreMigrations.All);
        var meta = new OdAppMeta(database);

        Assert.Null(meta.Get(OdAppMetaKeys.LegalAcknowledgedVersion));
        meta.Set(OdAppMetaKeys.LegalAcknowledgedVersion, "1.0");
        meta.Set(OdAppMetaKeys.LegalAcknowledgedVersion, "1.1");

        Assert.Equal("1.1", meta.Get(OdAppMetaKeys.LegalAcknowledgedVersion));
        Assert.Equal("1", meta.Get(OdAppMetaKeys.SchemaVersion));
    }

    private static OdDatabase NewDatabase(OdTempDirectory temp) => new(temp.Combine("omnideck.db"), pooling: false);
}
