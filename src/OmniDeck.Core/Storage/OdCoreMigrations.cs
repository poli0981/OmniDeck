using System.Globalization;
using System.Reflection;

namespace OmniDeck.Core.Storage;

/// <summary>
/// The core schema: <c>Storage/Migrations/NNNN_name.sql</c> files embedded in this assembly. A new
/// step is a new file with the next number; shipped files never change.
/// </summary>
public static class OdCoreMigrations
{
    private const string Prefix = "OmniDeck.Core.Storage.Migrations.";

    public static IReadOnlyList<OdMigration> All { get; } = Load(typeof(OdCoreMigrations).Assembly);

    internal static IReadOnlyList<OdMigration> Load(Assembly assembly)
    {
        var migrations = new List<OdMigration>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(Prefix, StringComparison.Ordinal) || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resource[Prefix.Length..^".sql".Length];
            var underscore = fileName.IndexOf('_', StringComparison.Ordinal);
            var version = int.Parse(fileName[..underscore], NumberStyles.None, CultureInfo.InvariantCulture);
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            migrations.Add(new OdMigration(version, fileName[(underscore + 1)..], reader.ReadToEnd()));
        }

        return [.. migrations.OrderBy(m => m.Version)];
    }
}
