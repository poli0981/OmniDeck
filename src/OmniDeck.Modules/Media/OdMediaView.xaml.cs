using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

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

    /// <summary>The window covers the screen, or stops: the screen follows.</summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == OdFullscreen.IsActiveProperty && ViewModel is { } model)
        {
            model.IsFullscreen = (bool)e.NewValue;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OdMediaViewModel old)
        {
            old.PlayerChanged -= OnPlayerChanged;
        }

        if (e.NewValue is OdMediaViewModel model)
        {
            model.PlayerChanged += OnPlayerChanged;
            model.IsFullscreen = OdFullscreen.GetIsActive(this);
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

    private void OnQueueKeyDown(object sender, KeyEventArgs e)
    {
        if (Queue.SelectedItem is not OdMediaItem item || ViewModel is not { } model)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            model.PlayItemCommand.Execute(item);
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            model.RemoveItemCommand.Execute(item);
        }
    }

    // libVLC's overlay is a window of its own, which screen readers name: after the module, not after the library.
    private void OnOverlayLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow((DependencyObject)sender) is { } overlay && overlay != Window.GetWindow(this))
        {
            overlay.SetBinding(Window.TitleProperty, new Binding("[Media_Name]") { Source = OdLocalization.Instance, Mode = BindingMode.OneWay });
        }
    }

    // The overlay lives in a window of its own: requests go out from the view itself.
    private void OnFullscreenClick(object sender, RoutedEventArgs e) => OdFullscreen.Request(this, !OdFullscreen.GetIsActive(this));

    private void OnVideoMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Esc works once the picture was clicked: the overlay is a window of its own.
        Keyboard.Focus((IInputElement)sender);
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            OdFullscreen.Request(this, !OdFullscreen.GetIsActive(this));
        }
    }

    private void OnVideoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && OdFullscreen.GetIsActive(this))
        {
            e.Handled = true;
            OdFullscreen.Request(this, on: false);
        }
    }

    // The menu is built when it opens: the tracks and devices are those of the file playing now.
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        var subtitles = Submenu(OdLocalization.Get("Media_Subtitles"), model.SubtitleTracks.Select(track => Choice(track.Name, track.Id == model.SubtitleTrack, () => model.SetSubtitleTrack(track.Id))));
        subtitles.Items.Add(new Separator());
        subtitles.Items.Add(Choice(OdLocalization.Get("Media_AddSubtitle"), isChecked: false, AddSubtitleFile, enabled: model.Player is not null));
        subtitles.IsEnabled = true;

        IEnumerable<MenuItem> devices =
        [
            Choice(OdLocalization.Get("Media_DefaultDevice"), model.AudioDevice is null, () => model.SetAudioDevice(null)),
            .. model.AudioDevices.Select(device => Choice(device.Name, string.Equals(device.Id, model.AudioDevice, StringComparison.Ordinal), () => model.SetAudioDevice(device.Id))),
        ];

        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Top };
        menu.Items.Add(Submenu(OdLocalization.Get("Media_AudioTrack"), model.AudioTracks.Select(track => Choice(track.Name, track.Id == model.AudioTrack, () => model.SetAudioTrack(track.Id)))));
        menu.Items.Add(subtitles);
        menu.Items.Add(Submenu(OdLocalization.Get("Media_SubtitleEncoding"), OdSubtitleEncodings.All.Select(encoding => Choice(OdSubtitleEncodings.Title(encoding), string.Equals(encoding, model.SubtitleEncoding, StringComparison.Ordinal), () => model.SetSubtitleEncoding(encoding)))));
        menu.Items.Add(Submenu(OdLocalization.Get("Media_Speed"), OdMediaSpeeds.All.Select(rate => Choice(OdMediaSpeeds.Title(rate), Math.Abs(rate - model.Rate) < 0.001f, () => model.SetRate(rate)))));
        menu.Items.Add(Submenu(OdLocalization.Get("Media_AudioDevice"), devices));
        menu.Items.Add(new Separator());
        menu.Items.Add(Choice(OdLocalization.Get("Media_Snapshot"), isChecked: false, () => model.TakeSnapshotCommand.Execute(null), enabled: model.ShowsVideo));
        menu.IsOpen = true;
    }

    private static MenuItem Submenu(string header, IEnumerable<MenuItem> items)
    {
        var submenu = new MenuItem { Header = header };
        foreach (var item in items)
        {
            submenu.Items.Add(item);
        }

        submenu.IsEnabled = submenu.Items.Count > 0;
        return submenu;
    }

    private static MenuItem Choice(string header, bool isChecked, Action choose, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsChecked = isChecked, IsEnabled = enabled };
        item.Click += (_, _) => choose();
        return item;
    }

    private void AddSubtitleFile()
    {
        var dialog = new OpenFileDialog { Filter = OdLocalization.Get("Media_SubtitleFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ViewModel?.AddSubtitle(dialog.FileName);
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
