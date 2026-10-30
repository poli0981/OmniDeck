using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;

namespace OmniDeck.App.Shell;

/// <summary>
/// The task-manager panel (docs/RAM-GOVERNOR.md §5): every module with its state and last use, the
/// memory OmniDeck uses against its limits, and the governor's own Suspend, Hibernate, Close and
/// keep-running controls. It acts through the governor, never around it. Used from the UI thread.
/// </summary>
public sealed partial class OdMemoryViewModel : ObservableObject, IDisposable
{
    private readonly OdModuleHost _host;
    private readonly OdRamGovernor _governor;
    private readonly IOdSettingsService _settings;
    private readonly TimeProvider _time;

    [ObservableProperty]
    private string _inUseText = "";

    [ObservableProperty]
    private string _limitsText = "";

    public OdMemoryViewModel(OdModuleHost host, OdRamGovernor governor, IOdSettingsService settings, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(governor);
        ArgumentNullException.ThrowIfNull(settings);
        _host = host;
        _governor = governor;
        _settings = settings;
        _time = time;
        Rows = [.. host.Modules.Select(hosted => new OdMemoryRowViewModel(hosted))];
        host.StateChanged += OnModuleStateChanged;
        governor.Sampled += OnSampled;
        settings.Changed += OnSettingsChanged;
        OdLocalization.Instance.CultureChanged += OnCultureChanged;
        Refresh();
    }

    public IReadOnlyList<OdMemoryRowViewModel> Rows { get; }

    public bool HasModules => Rows.Count > 0;

    public void Refresh()
    {
        var now = _time.GetUtcNow();
        foreach (var row in Rows)
        {
            row.Refresh(now, _governor.IsKeptRunning(row.Id));
        }

        var options = _governor.Options;
        InUseText = OdLocalization.Format("Memory_InUse", OdBytes.Format(_governor.CommittedBytes));
        LimitsText = OdLocalization.Format("Memory_Limits", OdBytes.Format(options.SoftCapBytes), OdBytes.Format(options.HardCapBytes));
    }

    public void Dispose()
    {
        _host.StateChanged -= OnModuleStateChanged;
        _governor.Sampled -= OnSampled;
        _settings.Changed -= OnSettingsChanged;
        OdLocalization.Instance.CultureChanged -= OnCultureChanged;
    }

    [RelayCommand]
    private Task SuspendAsync(OdMemoryRowViewModel row) => _governor.SuspendAsync(row.Id);

    [RelayCommand]
    private Task HibernateAsync(OdMemoryRowViewModel row) => _governor.HibernateAsync(row.Id);

    [RelayCommand]
    private Task CloseAsync(OdMemoryRowViewModel row) => _governor.CloseAsync(row.Id);

    [RelayCommand]
    private void ToggleKeepRunning(OdMemoryRowViewModel row) => _governor.SetKeptRunning(row.Id, !_governor.IsKeptRunning(row.Id));

    private void OnModuleStateChanged(object? sender, OdModuleStateChangedEventArgs e) => Refresh();

    private void OnSampled(object? sender, OdGovernorSampledEventArgs e) => Refresh();

    private void OnSettingsChanged(object? sender, OdSettingsChangedEventArgs e) => Refresh();

    private void OnCultureChanged(object? sender, EventArgs e) => Refresh();
}
