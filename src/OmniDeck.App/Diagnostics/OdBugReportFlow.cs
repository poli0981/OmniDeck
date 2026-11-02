using System.ComponentModel;
using System.IO;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Reports;
using Serilog;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Reporting a bug (docs/legal/PRIVACY.md §3): the report is previewed in a dialog. On the user's
/// say-so it is saved as a zip, File Explorer shows it, and the browser opens a new GitHub issue with
/// the form's short fields filled in. Nothing is uploaded: the user attaches the zip.
/// </summary>
public sealed class OdBugReportFlow(OdDialogs dialogs, OdBugReporter reporter, OdBugReports reports, OdModuleHost host, ILogger log)
{
    /// <summary>Where new issues are filed.</summary>
    public const string NewIssue = OdAboutViewModel.Repository + "/issues/new";

    private readonly ILogger _log = log.ForContext<OdBugReportFlow>();

    /// <summary>The new-issue page with the form's short fields filled in; the long ones are in the zip's issue.md.</summary>
    public static Uri IssueUrl(OdBugReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new Uri(NewIssue
            + "?template=bug_report.yml"
            + "&version=" + Uri.EscapeDataString(report.Version)
            + "&os=" + Uri.EscapeDataString(report.Windows)
            + "&preset=" + Uri.EscapeDataString(report.Preset)
            + "&module=" + Uri.EscapeDataString(report.Subject));
    }

    /// <summary>Shows the dialog; <paramref name="error"/> is the error OmniDeck showed, when the report comes from it.</summary>
    public async Task ShowAsync(string? error)
    {
        if (!dialogs.CanShow)
        {
            return;
        }

        var model = new OdBugReportViewModel(reporter, host.Current?.Info.Id ?? "", error);
        var answer = await dialogs.ShowAsync(new ContentDialog
        {
            Title = OdLocalization.Get("Report_Title"),
            Content = new OdBugReportView { DataContext = model },
            PrimaryButtonText = OdLocalization.Get("Report_Save"),
            CloseButtonText = OdLocalization.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            DialogMaxWidth = 760,
        });
        if (answer != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var path = reports.Save(model.Report);
            _log.Information("Saved a bug report as {File}", Path.GetFileName(path));
            OdExternal.ShowFile(path);
            OdExternal.Open(IssueUrl(model.Report));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            _log.Warning(ex, "The bug report could not be saved or shown");
        }
    }
}
