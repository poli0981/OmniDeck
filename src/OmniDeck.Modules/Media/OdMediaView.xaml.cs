using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Media;

/// <summary>The media player's view: libVLC's video, its controls, and the queue. The file dialogs live here.</summary>
public partial class OdMediaView
{
    public OdMediaView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private OdMediaViewModel? ViewModel => DataContext as OdMediaViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OdMediaViewModel old)
        {
            old.PlayerChanged -= OnPlayerChanged;
        }

        if (e.NewValue is OdMediaViewModel model)
        {
            model.PlayerChanged += OnPlayerChanged;
            ShowPlayer(model);
        }
    }

    private void OnPlayerChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } model)
        {
            ShowPlayer(model);
        }
    }

    private void ShowPlayer(OdMediaViewModel model) => Video.MediaPlayer = (model.Player as OdVlcPlayer)?.Native;

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = OdMediaFiles.Filter(OdLocalization.Get("Media_FilesFilter"), OdLocalization.Get("Media_AllFilter")) + "|" + OdLocalization.Get("Media_PlaylistFilter"),
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.Add(dialog.FileNames);
        }
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.Add([dialog.FolderName]);
        }
    }

    private void OnSavePlaylist(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = OdLocalization.Get("Media_PlaylistFilter"), FileName = "OmniDeck.m3u" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.SavePlaylist(dialog.FileName);
        }
    }

    private void OnQueueDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Queue.SelectedItem is OdMediaItem item)
        {
            ViewModel?.PlayItemCommand.Execute(item);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            ViewModel?.Add(paths);
        }
    }
}
