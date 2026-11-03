using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Settings;

/// <summary>
/// First-run setup, right after the legal gate: the language and the UI preset. Each preset card
/// previews its preset with the layer the theme engine builds for it, and a preset applies to the
/// whole window as soon as it is picked. Get started records that setup is done.
/// </summary>
public sealed partial class OdOnboardingViewModel : ObservableObject
{
    private readonly IOdSettingsService _settings;
    private readonly IOdAppMeta _meta;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private string _preset;

    public OdOnboardingViewModel(IOdSettingsService settings, IOdAppMeta meta, Func<OdPreset, ResourceDictionary>? preview = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _meta = meta;
        _language = settings.Current.Language;
        _preset = settings.Current.Preset;
        Presets = OdPresetOption.All(preview);
    }

    /// <summary>Raised once the user is done.</summary>
    public event EventHandler? Finished;

    public IReadOnlyList<OdChoice<string>> Languages { get; } = OdSettingsViewModel.LanguageChoices();

    public IReadOnlyList<OdPresetOption> Presets { get; }

    /// <summary>Whether this is the first run, so setup still has to happen.</summary>
    public static bool IsNeeded(IOdAppMeta meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return meta.Get(OdAppMetaKeys.OnboardingDone) is null;
    }

    [RelayCommand]
    private void Finish()
    {
        _meta.Set(OdAppMetaKeys.OnboardingDone, "1");
        Finished?.Invoke(this, EventArgs.Empty);
    }

    partial void OnLanguageChanged(string value) => _settings.Update(s => s with { Language = value });

    partial void OnPresetChanged(string value) => _settings.Update(s => s with { Preset = value });
}
