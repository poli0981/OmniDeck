using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Localization;

namespace OmniDeck.App.Legal;

/// <summary>
/// The legal gate (docs/SPEC.md §2): the shipped documents, a "read them" check, and Continue or
/// Quit. Continue records the acknowledgment; it is not a condition of the GPL. With
/// <c>readOnly</c>, the same documents open from About, with nothing to acknowledge.
/// </summary>
public sealed partial class OdLegalViewModel : ObservableObject
{
    private readonly OdLegalGate _gate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _hasRead;

    public OdLegalViewModel(OdLegalGate gate, string legalFolder, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(legalFolder);
        _gate = gate;
        IsReadOnly = readOnly;
        Pages = [.. OdLegal.Documents.Select(document => new OdLegalPage(OdLocalization.Get(document.TitleKey), Read(Path.Combine(legalFolder, document.FileName))))];
    }

    /// <summary>Raised when the user continues or quits; <see cref="Accepted"/> says which.</summary>
    public event EventHandler? Finished;

    public IReadOnlyList<OdLegalPage> Pages { get; }

    public bool IsReadOnly { get; }

    public bool AsksForAcknowledgment => !IsReadOnly;

    /// <summary>"Quit" at the gate; "Close" when the documents were opened to read them again.</summary>
    public string QuitText => IsReadOnly ? OdLocalization.Get("Legal_Close") : OdLocalization.Get("Legal_Quit");

    /// <summary>True after Continue, false after Quit, null while undecided.</summary>
    public bool? Accepted { get; private set; }

    private static string Read(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : OdLocalization.Format("Legal_Missing", Path.GetFileName(path));

    [RelayCommand(CanExecute = nameof(HasRead))]
    private void Continue()
    {
        _gate.Acknowledge();
        Finish(accepted: true);
    }

    [RelayCommand]
    private void Quit() => Finish(accepted: false);

    private void Finish(bool accepted)
    {
        Accepted = accepted;
        Finished?.Invoke(this, EventArgs.Empty);
    }
}
