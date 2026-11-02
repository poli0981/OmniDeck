using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.App.Settings;
using OmniDeck.Core.Reports;

namespace OmniDeck.App.Diagnostics;

/// <summary>The bug-report dialog: what the report is about, and the issue text it will carry.</summary>
public sealed partial class OdBugReportViewModel : ObservableObject
{
    private readonly OdBugReporter _reporter;
    private readonly string? _error;

    [ObservableProperty]
    private string _subject;

    [ObservableProperty]
    private string _preview;

    /// <summary>The dialog's model, about <paramref name="subject"/> to begin with.</summary>
    /// <param name="reporter">Composes the report.</param>
    /// <param name="subject">The module the report starts out about; empty for OmniDeck in general.</param>
    /// <param name="error">The error OmniDeck showed, when the report comes from the error dialog.</param>
    public OdBugReportViewModel(OdBugReporter reporter, string subject, string? error)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        _reporter = reporter;
        _error = error;
        Subjects = reporter.Subjects();
        _subject = Subjects.Any(choice => string.Equals(choice.Value, subject, StringComparison.Ordinal)) ? subject : "";
        Report = reporter.Compose(_subject, error);
        _preview = Report.Issue;
    }

    public IReadOnlyList<OdChoice<string>> Subjects { get; }

    /// <summary>The report as it stands, for the subject chosen.</summary>
    public OdBugReport Report { get; private set; }

    partial void OnSubjectChanged(string value)
    {
        Report = _reporter.Compose(value, _error);
        Preview = Report.Issue;
    }
}
