using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Calendar;

/// <summary>The calendar screen, bound to <see cref="OdCalendarViewModel"/>; it owns the file dialogs for .ics files.</summary>
public partial class OdCalendarView
{
    public OdCalendarView()
    {
        InitializeComponent();
    }

    private OdCalendarViewModel ViewModel => (OdCalendarViewModel)DataContext;

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = OdLocalization.Get("Calendar_IcsFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            ViewModel.Import(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            ViewModel.Report(OdLocalization.Get("Calendar_ImportFailed"));
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = OdLocalization.Get("Calendar_IcsFilter"), FileName = "OmniDeck.ics" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, ViewModel.Export(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ViewModel.Report(OdLocalization.Format("Calendar_Exported", Path.GetFileName(dialog.FileName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.Report(OdLocalization.Get("Calendar_ExportFailed"));
        }
    }
}
