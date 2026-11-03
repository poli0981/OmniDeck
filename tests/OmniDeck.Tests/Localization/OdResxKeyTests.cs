using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using OmniDeck.App.Resources;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Core.Theming;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Calendar;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Media;
using OmniDeck.Modules.Resources;
using OmniDeck.Modules.Stocks;
using OmniDeck.Modules.Timer;
using OmniDeck.Modules.Weather;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.Stocks;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Resources;

namespace OmniDeck.Tests.Localization;

/// <summary>
/// Cross-checks each string table against the sources that read it: a key the UI uses must exist,
/// and a key nothing uses is dead weight for every translator. The shell's table serves the app and
/// Core; the modules and the web runner have their own.
/// </summary>
public sealed partial class OdResxKeyTests
{
    public const string Shell = "shell";
    public const string BuiltInModules = "modules";
    public const string WebRunner = "web";

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static TheoryData<string> Tables => new(Shell, BuiltInModules, WebRunner);

    [Theory]
    [MemberData(nameof(Tables))]
    public void EveryKeyTheSourcesUse_IsInTheirStringTable(string table)
    {
        var strings = StringTable(table);
        var used = UsedKeys(table);

        Assert.True(used.Count > (IsWeb(table) ? 3 : 15), "The scan should find the sources' keys.");
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

    private static bool IsWeb(string table) => string.Equals(table, WebRunner, StringComparison.Ordinal);

    private static Dictionary<string, string> StringTable(string table)
    {
        // A manager of our own: disposing the set must not touch the one OdLocalization reads from.
        var manager = table switch
        {
            Shell => new ResourceManager("OmniDeck.App.Resources.Strings", typeof(OdAppStrings).Assembly),
            WebRunner => new ResourceManager("OmniDeck.Web.Resources.Strings", typeof(OdWebStrings).Assembly),
            _ => new ResourceManager("OmniDeck.Modules.Resources.Strings", typeof(OdModuleStrings).Assembly),
        };
        using var set = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)
            ?? throw new InvalidOperationException($"The {table} string table is missing.");
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    }

    private static SortedSet<string> UsedKeys(string table)
    {
        string[] projects = table switch
        {
            Shell => ["OmniDeck.App", "OmniDeck.Core"],
            WebRunner => ["OmniDeck.Web"],
            _ => ["OmniDeck.Modules"],
        };
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
        else if (IsWeb(table))
        {
            // Each web app's module names its category by key (OdWebAppModule); the permission questions
            // and the names of the kinds are built from the kinds OmniDeck asks about (OdWebPermissions).
            keys.Add("Category_Web");
            keys.UnionWith(OdWebPermissions.Asked.SelectMany(kind => new[] { "Web_Ask" + kind, "Web_Kind" + kind }));
        }
        else
        {
            // Names and categories travel as keys in OdModuleInfo; the shell resolves them.
            using var timers = new OdTimerService(TimeProvider.System, new OdFakeNotifier());
            var calendar = new OdCalendarModule(new OdCalendarStore(new OdDatabase(Path.Combine(Path.GetTempPath(), "unused.db"), pooling: false)));
            IOdModule[] modules = [new OdClockModule(), new OdTimerModule(timers), new OdCalculatorModule(), new OdWeatherModule(), new OdStocksModule(new OdMemorySecrets()), calendar, new OdMediaModule()];
            keys.UnionWith(modules.SelectMany(module => new[] { module.Info.NameKey, module.Info.CategoryKey }));
        }

        return keys;
    }

    // {res:OdLoc Key} in XAML; OdLocalization.Get("Key"), .Format("Key", ...) and Localized("Key") in code.
    [GeneratedRegex(@"(?:OdLoc\s+|(?:Get|Format|Localized)\("")(?<key>[A-Z][A-Za-z]*_[A-Za-z0-9_]+)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UiKey();
}
