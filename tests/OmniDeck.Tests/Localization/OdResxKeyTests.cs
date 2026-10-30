using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using OmniDeck.App.Resources;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Localization;

/// <summary>
/// Cross-checks the shell's sources against <c>Resources/Strings.resx</c>: a key the UI uses must
/// exist, and a key nothing uses is dead weight for every translator.
/// </summary>
public sealed partial class OdResxKeyTests
{
    private static readonly string[] ScannedProjects = ["OmniDeck.App", "OmniDeck.Core"];

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    [Fact]
    public void EveryKeyTheShellUses_IsInTheStringTable()
    {
        var table = StringTable();

        var missing = UsedKeys().Where(key => !table.ContainsKey(key)).ToList();

        Assert.True(UsedKeys().Count > 15, "The scan should find the shell's keys.");
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryKeyInTheStringTable_IsUsed()
    {
        var used = UsedKeys();

        Assert.DoesNotContain(StringTable().Keys, key => !used.Contains(key));
    }

    private static Dictionary<string, string> StringTable()
    {
        // A manager of our own: disposing the set must not touch the one OdLocalization reads from.
        var manager = new ResourceManager("OmniDeck.App.Resources.Strings", typeof(OdAppStrings).Assembly);
        using var set = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)
            ?? throw new InvalidOperationException("The shell's string table is missing.");
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    private static SortedSet<string> UsedKeys()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var files = ScannedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(OdRepository.Root, "src", project), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Separators).Any(part => part is "bin" or "obj"));
        foreach (var match in files.SelectMany(File.ReadLines).SelectMany(line => UiKey().Matches(line)))
        {
            keys.Add(match.Groups["key"].Value);
        }

        return keys;
    }

    // {res:OdLoc Key} in XAML; OdLocalization.Get("Key"), .Format("Key", ...) and Localized("Key") in code.
    [GeneratedRegex(@"(?:OdLoc\s+|(?:Get|Format|Localized)\("")(?<key>[A-Z][A-Za-z]*_[A-Za-z_]+)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UiKey();
}
