using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using OmniDeck.App.Resources;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Theming;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Resources;
using OmniDeck.Modules.Timer;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Localization;

/// <summary>
/// Cross-checks each string table against the sources that read it: a key the UI uses must exist,
/// and a key nothing uses is dead weight for every translator. The shell's table serves the app and
/// Core; the modules have their own.
/// </summary>
public sealed partial class OdResxKeyTests
{
    public const string Shell = "shell";
    public const string BuiltInModules = "modules";

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static TheoryData<string> Tables => new(Shell, BuiltInModules);

    [Theory]
    [MemberData(nameof(Tables))]
    public void EveryKeyTheSourcesUse_IsInTheirStringTable(string table)
    {
        var strings = StringTable(table);
        var used = UsedKeys(table);

        Assert.True(used.Count > 15, "The scan should find the sources' keys.");
        Assert.DoesNotContain(used, key => !strings.ContainsKey(key));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void EveryKeyInAStringTable_IsUsed(string table)
    {
        var used = UsedKeys(table);

        Assert.DoesNotContain(StringTable(table).Keys, key => !used.Contains(key));
    }

    private static bool IsShell(string table) => string.Equals(table, Shell, StringComparison.Ordinal);

    private static Dictionary<string, string> StringTable(string table)
    {
        // A manager of our own: disposing the set must not touch the one OdLocalization reads from.
        var manager = IsShell(table)
            ? new ResourceManager("OmniDeck.App.Resources.Strings", typeof(OdAppStrings).Assembly)
            : new ResourceManager("OmniDeck.Modules.Resources.Strings", typeof(OdModuleStrings).Assembly);
        using var set = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)
            ?? throw new InvalidOperationException($"The {table} string table is missing.");
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    private static SortedSet<string> UsedKeys(string table)
    {
        string[] projects = IsShell(table) ? ["OmniDeck.App", "OmniDeck.Core"] : ["OmniDeck.Modules"];
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var files = projects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(OdRepository.Root, "src", project), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Separators).Any(part => part is "bin" or "obj"));
        foreach (var match in files.SelectMany(File.ReadLines).SelectMany(line => UiKey().Matches(line)))
        {
            keys.Add(match.Groups["key"].Value);
        }

        if (IsShell(table))
        {
            // Built at run time: state names (OdMemoryRowViewModel), preset cards (OdPresetOption)
            // and the legal documents' titles (OdLegal).
            keys.UnionWith(Enum.GetNames<OdModuleState>().Select(state => "State_" + state));
            keys.UnionWith(OdPreset.All.SelectMany(preset => new[] { "Preset_" + preset.Name, "Preset_" + preset.Name + "Hint" }));
            keys.UnionWith(OdLegal.Documents.Select(document => document.TitleKey));
        }
        else
        {
            // Names and categories travel as keys in OdModuleInfo; the shell resolves them.
            using var timers = new OdTimerService(TimeProvider.System, new OdFakeNotifier());
            IOdModule[] modules = [new OdClockModule(), new OdTimerModule(timers), new OdCalculatorModule()];
            keys.UnionWith(modules.SelectMany(module => new[] { module.Info.NameKey, module.Info.CategoryKey }));
        }

        return keys;
    }

    // {res:OdLoc Key} in XAML; OdLocalization.Get("Key"), .Format("Key", ...) and Localized("Key") in code.
    [GeneratedRegex(@"(?:OdLoc\s+|(?:Get|Format|Localized)\("")(?<key>[A-Z][A-Za-z]*_[A-Za-z0-9_]+)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UiKey();
}
