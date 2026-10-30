using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.App.Shell;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;

namespace OmniDeck.App.Settings;

/// <summary>
/// The settings page (docs/SPEC.md §2): General, Appearance, Memory, Data and About. Every change is
/// saved at once through <see cref="IOdSettingsService"/>, and the page follows changes made
/// elsewhere (the tray, an import). Used from the UI thread.
/// </summary>
public sealed partial class OdSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IOdSettingsService _settings;
    private readonly IOdStartupRegistration _startup;
    private readonly Action<string> _openFolder;
    private bool _loading;

    /// <summary>The selected tab, as an <see cref="OdSettingsSection"/> index.</summary>
    [ObservableProperty]
    private int _sectionIndex;

    [ObservableProperty]
    private string _language = "";

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private OdThemeMode _theme;

    [ObservableProperty]
    private string _preset = "";

    [ObservableProperty]
    private double _fontScale = 1;

    [ObservableProperty]
    private bool _motionEnabled;

    [ObservableProperty]
    private bool _useSystemAccent;

    [ObservableProperty]
    private string _accentColor = "";

    // Doubles because WPF-UI's NumberBox edits a double?.
    [ObservableProperty]
    private double? _idleAfterMinutes;

    [ObservableProperty]
    private double? _hibernateAfterMinutes;

    [ObservableProperty]
    private double? _softCapMegabytes;

    [ObservableProperty]
    private double? _hardCapMegabytes;

    [ObservableProperty]
    private string _dataMessage = "";

    public OdSettingsViewModel(
        IOdSettingsService settings,
        IOdStartupRegistration startup,
        OdDataRoot dataRoot,
        OdMemoryViewModel memory,
        OdAboutViewModel about,
        Action<string> openFolder)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dataRoot);
        _settings = settings;
        _startup = startup;
        _openFolder = openFolder;
        Memory = memory;
        About = about;
        DataFolder = dataRoot.Root;
        Load(settings.Current);
        settings.Changed += OnSettingsChanged;
    }

    public IReadOnlyList<OdChoice<string>> Languages { get; } = LanguageChoices();

    public IReadOnlyList<OdChoice<OdThemeMode>> Themes { get; } =
    [
        new(OdThemeMode.System, OdLocalization.Get("Theme_System")),
        new(OdThemeMode.Light, OdLocalization.Get("Theme_Light")),
        new(OdThemeMode.Dark, OdLocalization.Get("Theme_Dark")),
    ];

    public IReadOnlyList<OdPresetOption> Presets { get; } = OdPresetOption.All();

    /// <summary>Accent colors to pick from when the Windows accent is not used.</summary>
    public IReadOnlyList<string> AccentSwatches { get; } = ["#0067C0", "#8764B8", "#C239B3", "#E74856", "#CA5010", "#10893E"];

    public OdMemoryViewModel Memory { get; }

    public OdAboutViewModel About { get; }

    public string DataFolder { get; }

    /// <summary>The language choices: Windows' display language, then the languages OmniDeck ships.</summary>
    public static IReadOnlyList<OdChoice<string>> LanguageChoices() =>
    [
        new("", OdLocalization.Get("Language_System")),
        new("en", OdLocalization.Get("Language_English")),
    ];

    public void ShowSection(OdSettingsSection section) => SectionIndex = (int)section;

    public void ExportTo(string path)
    {
        _settings.Export(path);
        DataMessage = OdLocalization.Get("Settings_Exported");
    }

    public void ImportFrom(string path) =>
        DataMessage = _settings.Import(path) ? OdLocalization.Get("Settings_Imported") : OdLocalization.Get("Settings_ImportFailed");

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    [RelayCommand]
    private void OpenDataFolder() => _openFolder(DataFolder);

    [RelayCommand]
    private void ChooseAccent(string color)
    {
        AccentColor = color;
        UseSystemAccent = false;
    }

    partial void OnSectionIndexChanged(int value)
    {
        if (value == (int)OdSettingsSection.About)
        {
            _ = About.CheckOnOpenAsync();
        }
    }

    partial void OnLanguageChanged(string value) => Apply(s => s with { Language = value });

    partial void OnCloseToTrayChanged(bool value) => Apply(s => s with { CloseToTray = value });

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
        {
            _startup.Set(value);
            Apply(s => s with { StartWithWindows = value });
        }
    }

    partial void OnThemeChanged(OdThemeMode value) => Apply(s => s with { Theme = value });

    partial void OnPresetChanged(string value) => Apply(s => s with { Preset = value });

    partial void OnFontScaleChanged(double value) => Apply(s => s with { FontScale = value });

    partial void OnMotionEnabledChanged(bool value) => Apply(s => s with { MotionEnabled = value });

    partial void OnUseSystemAccentChanged(bool value)
    {
        // Leaving the Windows accent keeps the color already picked, or starts from the first swatch.
        var custom = AccentColor.Length > 0 ? AccentColor : AccentSwatches[0];
        Apply(s => s with { AccentColor = value ? "" : custom });
    }

    partial void OnAccentColorChanged(string value) => Apply(s => s with { AccentColor = UseSystemAccent ? "" : value });

    partial void OnIdleAfterMinutesChanged(double? value) =>
        Apply(s => s with { Memory = s.Memory with { IdleAfterMinutes = WholeNumber(value, s.Memory.IdleAfterMinutes) } });

    partial void OnHibernateAfterMinutesChanged(double? value) =>
        Apply(s => s with { Memory = s.Memory with { HibernateAfterMinutes = WholeNumber(value, s.Memory.HibernateAfterMinutes) } });

    partial void OnSoftCapMegabytesChanged(double? value) =>
        Apply(s => s with { Memory = s.Memory with { SoftCapMegabytes = WholeNumber(value, 0) } });

    partial void OnHardCapMegabytesChanged(double? value) =>
        Apply(s => s with { Memory = s.Memory with { HardCapMegabytes = WholeNumber(value, 0) } });

    private static int WholeNumber(double? value, int fallback) =>
        value is { } number && double.IsFinite(number) ? (int)Math.Clamp(Math.Floor(number), 0, int.MaxValue) : fallback;

    private void Apply(Func<OdSettings, OdSettings> change)
    {
        if (!_loading)
        {
            _settings.Update(change);
        }
    }

    private void OnSettingsChanged(object? sender, OdSettingsChangedEventArgs e) => Load(e.Settings);

    /// <summary>Shows <paramref name="settings"/> without writing anything back.</summary>
    private void Load(OdSettings settings)
    {
        _loading = true;
        try
        {
            Language = settings.Language;
            CloseToTray = settings.CloseToTray;
            StartWithWindows = _startup.IsEnabled;
            Theme = settings.Theme;
            Preset = settings.Preset;
            FontScale = settings.FontScale;
            MotionEnabled = settings.MotionEnabled;
            UseSystemAccent = settings.AccentColor.Length == 0;
            AccentColor = settings.AccentColor;
            IdleAfterMinutes = settings.Memory.IdleAfterMinutes;
            HibernateAfterMinutes = settings.Memory.HibernateAfterMinutes;
            SoftCapMegabytes = settings.Memory.SoftCapMegabytes;
            HardCapMegabytes = settings.Memory.HardCapMegabytes;
        }
        finally
        {
            _loading = false;
        }
    }
}
