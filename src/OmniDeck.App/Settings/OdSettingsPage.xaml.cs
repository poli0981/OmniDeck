using System.Windows;
using Microsoft.Win32;
using OmniDeck.Core.Localization;

namespace OmniDeck.App.Settings;

/// <summary>The settings page, bound to <see cref="OdSettingsViewModel"/>; the file dialogs live here.</summary>
public partial class OdSettingsPage
{
    public OdSettingsPage()
    {
        InitializeComponent();
    }

    private OdSettingsViewModel? ViewModel => DataContext as OdSettingsViewModel;

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = "omnideck-settings.json", Filter = OdLocalization.Get("Settings_FileFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.ExportTo(dialog.FileName);
        }
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = OdLocalization.Get("Settings_FileFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.ImportFrom(dialog.FileName);
        }
    }
}
