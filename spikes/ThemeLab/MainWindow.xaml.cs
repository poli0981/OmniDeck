using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using UiButton = Wpf.Ui.Controls.Button;
using UiCard = Wpf.Ui.Controls.Card;
using UiTextBox = Wpf.Ui.Controls.TextBox;

namespace ThemeLab;

public partial class MainWindow
{
    private const int WmSettingChange = 0x001A;

    // Kept alive for its TextScaleFactorChanged event.
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    private Preset _preset = Preset.Standard;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private double CurrentTextScale => ApplyTextScale.IsChecked == true ? TextScale.FromWinRt(_uiSettings) : 1.0;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        _uiSettings.TextScaleFactorChanged += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            Log($"UISettings.TextScaleFactorChanged -> {TextScale.FromWinRt(_uiSettings):0.00}");
            OnTextScaleChanged();
        });
        SystemEvents.UserPreferenceChanged += (_, args) => Dispatcher.InvokeAsync(() => Log($"SystemEvents.UserPreferenceChanged: {args.Category}"));

        UpdateTextScaleInfo();
        Log($"Started. Elements: {CountElements(this)}; preset {_preset.Name}; theme {ApplicationThemeManager.GetAppTheme()}; "
            + $"OS animations {(SystemParameters.ClientAreaAnimation ? "on" : "off")}; high contrast {(SystemParameters.HighContrast ? "on" : "off")}; "
            + $"text scale registry {TextScale.FromRegistry():0.00} / WinRT {TextScale.FromWinRt(_uiSettings):0.00}");

        if (Environment.GetCommandLineArgs().Contains("--auto"))
        {
            Dispatcher.InvokeAsync(RunAutomatedAsync, DispatcherPriority.ContextIdle);
        }
    }

    // `ThemeLab.exe --auto`: every measurement the lab can take without a human, written to
    // %LocalAppData%\OmniDeck.Spikes\theme\. Flicker and Mica artifacts still need eyes on the window.
    private async Task RunAutomatedAsync()
    {
        await ProbeAsync();
        await BenchmarkAsync();
        OnStress(this, new RoutedEventArgs());
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await BenchmarkAsync();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            OnThemeChecked(new FrameworkElement { Tag = theme }, new RoutedEventArgs());
            await Task.Delay(500);
        }

        OnFrozenStoryboardTest(this, new RoutedEventArgs());
        await Task.Delay(500);

        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDeck.Spikes", "theme");
        System.IO.Directory.CreateDirectory(dir);
        var file = System.IO.Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-auto.log");
        await System.IO.File.WriteAllTextAsync(file, LogBox.Text);
        Close();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSettingChange)
        {
            var area = lParam == IntPtr.Zero ? "(null)" : Marshal.PtrToStringUni(lParam);
            Log($"WM_SETTINGCHANGE wParam={wParam} lParam=\"{area}\"; registry text scale now {TextScale.FromRegistry():0.00}");
        }

        return IntPtr.Zero;
    }

    private async void OnPresetChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not FrameworkElement { Tag: string name })
        {
            return;
        }

        _preset = Preset.All.First(p => p.Name == name);
        var ms = await ApplyPresetAsync(_preset);
        Log($"Preset -> {_preset.Name}: {ms:F1} ms to layout/render ({CountElements(this)} elements)");
    }

    private async Task<double> ApplyPresetAsync(Preset preset)
    {
        var sw = Stopwatch.StartNew();
        PresetEngine.Apply(PresetEngine.Build(preset, CurrentTextScale));

        // Loaded priority runs after the pending layout and render passes.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        return sw.Elapsed.TotalMilliseconds;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        if (tag == "System")
        {
            SystemThemeWatcher.Watch(this);
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            SystemThemeWatcher.UnWatch(this);
            ApplicationThemeManager.Apply(tag == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark);
        }

        var presetStillMerged = Application.Current.Resources.MergedDictionaries.Any(d => d.Contains("Od.Preset.Name"));
        Log($"Theme -> {tag} ({ApplicationThemeManager.GetAppTheme()}) in {sw.Elapsed.TotalMilliseconds:F1} ms; preset layer still merged: {presetStillMerged}");
    }

    private async void OnTextScaleToggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await ApplyPresetAsync(_preset);
        UpdateTextScaleInfo();
        Log($"Text scale applied: {CurrentTextScale:0.00}");
    }

    private async void OnTextScaleChanged()
    {
        UpdateTextScaleInfo();
        if (ApplyTextScale.IsChecked == true)
        {
            await ApplyPresetAsync(_preset);
        }
    }

    private void UpdateTextScaleInfo() =>
        TextScaleInfo.Text = $"Registry: {TextScale.FromRegistry():0.00} · WinRT: {TextScale.FromWinRt(_uiSettings):0.00}";

    private async void OnBenchmark(object sender, RoutedEventArgs e) => await BenchmarkAsync();

    private async Task BenchmarkAsync()
    {
        var times = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            times.Add(await ApplyPresetAsync(Preset.All[i % Preset.All.Length]));
        }

        await ApplyPresetAsync(_preset);
        times.Sort();
        Log($"Benchmark x30 ({CountElements(this)} elements): min {times[0]:F1} ms, median {times[times.Count / 2]:F1} ms, "
            + $"p90 {times[(int)(times.Count * 0.9)]:F1} ms, max {times[^1]:F1} ms");
    }

    private void OnStress(object sender, RoutedEventArgs e)
    {
        for (var i = 0; i < 100; i++)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = $"Card {i}" });
            panel.Children.Add(new UiButton { Content = "Od button", Style = (Style)FindResource("OdButtonStyle") });
            panel.Children.Add(new UiTextBox { PlaceholderText = "Od text box", Style = (Style)FindResource("OdTextBoxStyle") });
            var card = new UiCard { Width = 220, Content = panel };
            card.SetResourceReference(MarginProperty, "Od.Margin.Item");
            StressHost.Children.Add(card);
        }

        Dispatcher.InvokeAsync(() => Log($"Stress: {StressHost.Children.Count} cards, {CountElements(this)} elements"), DispatcherPriority.Loaded);
    }

    private async void OnProbe(object sender, RoutedEventArgs e) => await ProbeAsync();

    private async Task ProbeAsync()
    {
        Log("Probe: preset | control | ActualHeight | Padding | FontSize | template CornerRadius");
        foreach (var preset in Preset.All)
        {
            await ApplyPresetAsync(preset);
            foreach (var (name, control) in new (string, Control)[]
            {
                ("default ui:Button", ProbeDefaultButton), ("Od ui:Button", ProbeOdButton),
                ("default ui:TextBox", ProbeDefaultTextBox), ("Od ui:TextBox", ProbeOdTextBox), ("ui:Card", ProbeCard),
            })
            {
                var border = FindFirst<Border>(control);
                Log($"  {preset.Name,-8} | {name,-18} | {control.ActualHeight,6:F1} | {control.Padding} | {control.FontSize,5:F1} | {border?.CornerRadius.ToString() ?? "-"}");
            }
        }

        await ApplyPresetAsync(_preset);
    }

    private void OnFrozenStoryboardTest(object sender, RoutedEventArgs e)
    {
        const string xaml = """
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
              <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Trigger.EnterActions>
                    <BeginStoryboard>
                      <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="Opacity" To="0.5" Duration="{DynamicResource Od.Motion.Duration}" />
                      </Storyboard>
                    </BeginStoryboard>
                  </Trigger.EnterActions>
                </Trigger>
              </Style.Triggers>
            </Style>
            """;
        try
        {
            var style = (Style)XamlReader.Parse(xaml);
            var button = new System.Windows.Controls.Button { Content = "Storyboard test", Style = style };
            StressHost.Children.Add(button);
            button.UpdateLayout();
            Log("Storyboard test: no exception (a DynamicResource in a style storyboard was accepted)");
        }
        catch (Exception ex)
        {
            Log($"Storyboard test: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnCopyLog(object sender, RoutedEventArgs e) => Clipboard.SetText(LogBox.Text);

    private void Log(string line)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private static int CountElements(DependencyObject root)
    {
        var count = 1;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            count += CountElements(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }

    private static T? FindFirst<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindFirst<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
