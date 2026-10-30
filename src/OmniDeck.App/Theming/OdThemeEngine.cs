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

    private void ApplyTheme()
    {
        var settings = _settings.Current;

        // High Contrast: palette tokens, the user's accent included, defer to the system entirely (docs/UI-MODES.md §4).
        var highContrast = SystemParameters.HighContrast;
        var systemAccent = settings.AccentColor.Length == 0 || highContrast;
        if (settings.Theme == OdThemeMode.System || highContrast)
        {
            ApplicationThemeManager.ApplySystemTheme(updateAccent: systemAccent);
        }
        else
        {
            var theme = settings.Theme == OdThemeMode.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: systemAccent);
        }

        if (!systemAccent && ColorConverter.ConvertFromString(settings.AccentColor) is Color accent)
        {
            ApplicationAccentColorManager.Apply(accent, ApplicationThemeManager.GetAppTheme());
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
            _ = _app?.Dispatcher.InvokeAsync(() =>
            {
                ApplyTheme();
                RefreshTextScale();
                ApplyTokens();
            });
        }
    }
}
