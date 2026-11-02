using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Theming;
using Serilog;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Theming;

/// <summary>
/// The theme engine (docs/UI-MODES.md): two independent axes, dark/light through WPF-UI's
/// <see cref="ApplicationThemeManager"/> and the UI preset through the <c>Od.*</c> token layer, both
/// following the settings, the Windows app mode, High Contrast and the Windows text size.
/// </summary>
public sealed class OdThemeEngine : IDisposable
{
    private readonly IOdSettingsService _settings;
    private readonly ILogger _log;
    private Application? _app;
    private OdSettings? _applied;
    private OdSystemLook? _systemLook;
    private double _textScale = 1.0;

    public OdThemeEngine(IOdSettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log.ForContext<OdThemeEngine>();
        Current = OdTokens.Compute(OdPreset.Standard, 1.0, 1.0, motionEnabled: true);
    }

    /// <summary>The tokens in effect.</summary>
    public OdTokens Current { get; private set; }

    /// <summary>Raised on the UI thread once the preset layer is rebuilt, for chrome that reads tokens in code.</summary>
    public event EventHandler? Applied;

    /// <summary>Applies the theme and the preset layer to <paramref name="app"/> and starts following changes.</summary>
    public void Start(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _app = app;
        _applied = _settings.Current;
        _systemLook = OdSystemLook.Read();
        _textScale = OdTextScale.Read();
        ApplyTheme();
        ApplyTokens();
        _settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Re-reads the Windows text size; windows call this on WM_SETTINGCHANGE and on activation.</summary>
    public void RefreshTextScale()
    {
        var now = OdTextScale.Read();
        if (Math.Abs(now - _textScale) > 0.001)
        {
            _textScale = now;
            ApplyTokens();
        }
    }

    /// <summary>
    /// Records Windows' look as it is <paramref name="now"/>; true when it differs from the look the
    /// theme was last applied for.
    /// </summary>
    internal bool NoteSystemLook(OdSystemLook now)
    {
        if (_systemLook == now)
        {
            return false;
        }

        _systemLook = now;
        return true;
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void ApplyTokens()
    {
        if (_app is null)
        {
            return;
        }

        var settings = _settings.Current;
        Current = OdTokens.Compute(
            OdPreset.FromName(settings.Preset),
            _textScale,
            settings.FontScale,
            settings.MotionEnabled && SystemParameters.ClientAreaAnimation);
        OdThemeResources.Apply(_app.Resources, OdThemeResources.Build(Current));
        _log.Debug("Preset {Preset} applied (text scale {TextScale}, font scale {FontScale})", Current.PresetName, _textScale, settings.FontScale);
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// What <see cref="ApplyTheme"/> applies: whether the theme follows Windows, the theme the user's
    /// accent is worked out for, and that accent, or null to use the Windows one.
    /// </summary>
    internal static (bool FollowSystem, ApplicationTheme Theme, Color? Accent) Choose(
        OdThemeMode mode,
        string accentColor,
        bool highContrast,
        Func<ApplicationTheme> systemTheme)
    {
        // High Contrast: palette tokens, the user's accent included, defer to the system entirely (docs/UI-MODES.md §4).
        var followSystem = mode == OdThemeMode.System || highContrast;
        Color? accent = !highContrast && accentColor.Length > 0 && ColorConverter.ConvertFromString(accentColor) is Color parsed ? parsed : null;
        var theme = mode switch
        {
            OdThemeMode.Light when !highContrast => ApplicationTheme.Light,
            OdThemeMode.Dark when !highContrast => ApplicationTheme.Dark,
            _ => systemTheme(),
        };
        return (followSystem, theme, accent);
    }

    private static ApplicationTheme SystemAppTheme()
    {
        SystemThemeManager.UpdateSystemThemeCache();
        return SystemThemeManager.GetCachedSystemTheme() switch
        {
            SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow => ApplicationTheme.Dark,
            SystemTheme.HC1 or SystemTheme.HC2 or SystemTheme.HCBlack or SystemTheme.HCWhite => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Light,
        };
    }

    private void ApplyTheme()
    {
        var settings = _settings.Current;
        var (followSystem, theme, accent) = Choose(settings.Theme, settings.AccentColor, SystemParameters.HighContrast, SystemAppTheme);

        // The user's accent goes first, as WPF-UI applies the Windows one: the theme dictionaries' accent
        // brushes (the toggle switches', for one) take the accent colors as they load, so an accent
        // applied after them showed up one pick late.
        if (accent is { } color)
        {
            ApplicationAccentColorManager.Apply(color, theme);
        }

        if (followSystem)
        {
            ApplicationThemeManager.ApplySystemTheme(updateAccent: accent is null);
        }
        else
        {
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: accent is null);
        }
    }

    private void OnSettingsChanged(object? sender, OdSettingsChangedEventArgs e)
    {
        // Pins, the language and other settings leave the look alone; re-applying it would re-style every window.
        if (_applied?.LooksLike(e.Settings) == true)
        {
            return;
        }

        _applied = e.Settings;
        _app?.Dispatcher.Invoke(() =>
        {
            ApplyTheme();
            ApplyTokens();
        });
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility or UserPreferenceCategory.VisualStyle)
        {
            // Windows sends this for many unrelated settings, and applying the theme again re-styles every
            // window, so it happens only when the look OmniDeck follows has changed.
            _ = _app?.Dispatcher.InvokeAsync(() =>
            {
                if (NoteSystemLook(OdSystemLook.Read()))
                {
                    ApplyTheme();
                    ApplyTokens();
                }

                RefreshTextScale();
            });
        }
    }
}
