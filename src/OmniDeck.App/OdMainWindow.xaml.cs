using System.Windows.Interop;
using OmniDeck.App.Theming;
using OmniDeck.Core;

namespace OmniDeck.App;

/// <summary>Placeholder shell window of the kickoff build; the real shell arrives in P1 WP4.</summary>
public partial class OdMainWindow
{
    private const int WmSettingChange = 0x001A;

    public OdMainWindow(OdThemeEngine theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        InitializeComponent();
        VersionText.Text = OdVersionInfo.FromAssembly(typeof(OdMainWindow).Assembly).DisplayText;

        // WPF ignores the Windows text size; re-read it when it may have changed (docs/UI-MODES.md §4).
        Activated += (_, _) => theme.RefreshTextScale();
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(
            (IntPtr _, int msg, IntPtr _, IntPtr _, ref bool _) =>
            {
                if (msg == WmSettingChange)
                {
                    theme.RefreshTextScale();
                }

                return IntPtr.Zero;
            });
    }
}
