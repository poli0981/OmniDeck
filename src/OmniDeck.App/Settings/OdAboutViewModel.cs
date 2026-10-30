using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.App.Updates;
using OmniDeck.Core;
using OmniDeck.Core.Localization;

namespace OmniDeck.App.Settings;

/// <summary>
/// Settings → About (docs/SPEC.md §2): version and commit, the update check (Velopack against GitHub
/// releases), and the project's links. Used from the UI thread.
/// </summary>
public sealed partial class OdAboutViewModel(IOdUpdateService updates, Action<Uri> open) : ObservableObject
{
    public const string Repository = "https://github.com/poli0981/OmniDeck";
    public const string Discussions = Repository + "/discussions";
    public const string Discord = "https://discord.gg/u8tn3YypyX";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    private OdUpdateState _state = OdUpdateState.Idle;

    [ObservableProperty]
    private string _statusText = "";

    public string VersionText { get; } = OdLocalization.Format("About_Version", OdVersionInfo.FromAssembly(typeof(OdAboutViewModel).Assembly).DisplayText);

    public bool CanDownload => State == OdUpdateState.Available;

    /// <summary>Raised when the user asks to read the legal documents again.</summary>
    public event EventHandler? LegalRequested;

    /// <summary>Opening About checks for updates (docs/ARCHITECTURE.md §7), unless a check already ran.</summary>
    public Task CheckOnOpenAsync() => State == OdUpdateState.Idle ? CheckCommand.ExecuteAsync(null) : Task.CompletedTask;

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (!updates.IsInstalled)
        {
            Show(OdUpdateState.NotInstalled, OdLocalization.Get("About_NotInstalled"));
            return;
        }

        Show(OdUpdateState.Checking, OdLocalization.Get("About_Checking"));
        try
        {
            var version = await updates.CheckAsync(CancellationToken.None);
            if (version is null)
            {
                Show(OdUpdateState.UpToDate, OdLocalization.Get("About_UpToDate"));
            }
            else
            {
                Show(OdUpdateState.Available, OdLocalization.Format("About_Available", version));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline, rate-limited or a broken release: say so and let the user try again.
            Show(OdUpdateState.Failed, OdLocalization.Format("About_Failed", ex.Message));
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        Show(OdUpdateState.Downloading, OdLocalization.Format("About_Downloading", 0));
        try
        {
            await updates.DownloadAsync(new Progress<int>(percent => StatusText = OdLocalization.Format("About_Downloading", percent)), CancellationToken.None);
            updates.ApplyAndRestart();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Show(OdUpdateState.Failed, OdLocalization.Format("About_Failed", ex.Message));
        }
    }

    [RelayCommand]
    private void OpenLink(string link) => open(new Uri(link, UriKind.Absolute));

    [RelayCommand]
    private void ShowLegal() => LegalRequested?.Invoke(this, EventArgs.Empty);

    private void Show(OdUpdateState state, string text)
    {
        State = state;
        StatusText = text;
    }
}
