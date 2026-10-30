using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

namespace OmniDeck.App.Shell;

/// <summary>One module in the task-manager panel (docs/RAM-GOVERNOR.md §5).</summary>
public sealed partial class OdMemoryRowViewModel(OdHostedModule hosted) : ObservableObject
{
    [ObservableProperty]
    private string _stateText = "";

    [ObservableProperty]
    private string _lastUsedText = "";

    [ObservableProperty]
    private bool _keepRunning;

    [ObservableProperty]
    private bool _canSuspend;

    [ObservableProperty]
    private bool _canHibernate;

    [ObservableProperty]
    private bool _canClose;

    public OdModuleInfo Info => hosted.Info;

    public string Id => hosted.Info.Id;

    public string Title => OdLocalization.Get(hosted.Info.NameKey);

    /// <summary>Re-reads the module's state; <paramref name="now"/> dates the last-used text.</summary>
    public void Refresh(DateTimeOffset now, bool keepRunning)
    {
        var state = hosted.State;
        StateText = OdLocalization.Get("State_" + state);
        LastUsedText = LastUsed(state, now - hosted.LastActive);
        KeepRunning = keepRunning;
        CanSuspend = state == OdModuleState.Idle;
        CanHibernate = state is OdModuleState.Idle or OdModuleState.Suspended;
        CanClose = state is not (OdModuleState.Created or OdModuleState.Closed);
        OnPropertyChanged(nameof(Title));
    }

    public override string ToString() => Title;

    private static string LastUsed(OdModuleState state, TimeSpan hiddenFor) => state switch
    {
        OdModuleState.Created => "",
        OdModuleState.Active => OdLocalization.Get("Memory_UsedNow"),
        _ when hiddenFor < TimeSpan.FromMinutes(1) => OdLocalization.Get("Memory_UsedJustNow"),
        _ when hiddenFor < TimeSpan.FromHours(1) => OdLocalization.Format("Memory_UsedMinutesAgo", (int)hiddenFor.TotalMinutes),
        _ => OdLocalization.Format("Memory_UsedHoursAgo", (int)hiddenFor.TotalHours),
    };
}
