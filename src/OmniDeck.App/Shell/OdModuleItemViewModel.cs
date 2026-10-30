using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

namespace OmniDeck.App.Shell;

/// <summary>A module as the sidebar, the library and the palette show it.</summary>
public sealed partial class OdModuleItemViewModel(OdModuleInfo info) : ObservableObject
{
    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private OdModuleState _state = OdModuleState.Created;

    public OdModuleInfo Info { get; } = info;

    public string Id => Info.Id;

    public string Title => OdLocalization.Get(Info.NameKey);

    public string Category => OdLocalization.Get(Info.CategoryKey);

    /// <summary>Opened and not closed since: it keeps a sidebar entry even when unpinned, like a tab.</summary>
    public bool IsOpen => State is not (OdModuleState.Created or OdModuleState.Closed);

    /// <summary>The name screen readers announce for the module's library tile and palette row.</summary>
    public override string ToString() => Title;

    /// <summary>Re-reads the localized texts after a language change.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Category));
    }
}
