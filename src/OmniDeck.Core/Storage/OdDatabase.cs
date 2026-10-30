using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace OmniDeck.Core.Storage;

/// <summary>
/// <c>omnideck.db</c> in the data root: SQLite through Dapper (docs/ARCHITECTURE.md §5). Hands out
/// open connections and brings the schema up to date with ordered, idempotent migrations.
/// </summary>
public sealed class OdDatabase
{
    private readonly string _connectionString;

    public OdDatabase(OdDataRoot dataRoot)
        : this(dataRoot?.DatabaseFile ?? throw new ArgumentNullException(nameof(dataRoot)), pooling: true)
    {
    }

    /// <summary>A database at an explicit path.</summary>
    /// <param name="path">The database file.</param>
    /// <param name="pooling">Off in tests, so their temp folders can be deleted right away.</param>
    internal OdDatabase(string path, bool pooling)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = pooling,
        }.ToString();
    }

    /// <summary>Releases every pooled connection, and with it the file; called on exit.</summary>
    public static void ReleaseAll() => SqliteConnection.ClearAllPools();

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;");
        return connection;
    }

    /// <summary>The schema version recorded in <c>app_meta</c>; 0 for an empty database.</summary>
    public int SchemaVersion()
    {
        using var connection = Open();
        return ReadSchemaVersion(connection);
    }

    /// <summary>Applies every migration newer than the current schema, each in its own transaction.</summary>
    /// <returns>How many migrations ran.</returns>
    public int Migrate(IReadOnlyList<OdMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var duplicate = migrations.GroupBy(m => m.Version).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Two migrations share version {duplicate.Key}.", nameof(migrations));
        }

        using var connection = Open();
        var current = ReadSchemaVersion(connection);
        var applied = 0;
        foreach (var migration in migrations.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            using var transaction = connection.BeginTransaction();
            connection.Execute(migration.Sql, transaction: transaction);
            connection.Execute(
                "INSERT INTO app_meta (key, value) VALUES (@key, @value) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
                new { key = OdAppMetaKeys.SchemaVersion, value = migration.Version.ToString(CultureInfo.InvariantCulture) },
                transaction);
            transaction.Commit();
            applied++;
        }

        return applied;
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        var hasMeta = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'app_meta'") > 0;
        if (!hasMeta)
        {
            return 0;
        }

        var value = connection.ExecuteScalar<string?>("SELECT value FROM app_meta WHERE key = @key", new { key = OdAppMetaKeys.SchemaVersion });
        return value is null ? 0 : int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
