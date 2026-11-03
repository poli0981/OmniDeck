using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace OmniDeck.Core.Accessibility;

/// <summary>
/// A status line that screen readers read out whenever its text changes once shown: a live region,
/// polite unless <see cref="AutomationProperties.LiveSettingProperty"/> says otherwise. WPF knows the
/// setting, but makes a change heard only when the element raises the event, which a TextBlock never does.
/// </summary>
public sealed class OdStatusText : TextBlock
{
    static OdStatusText()
    {
        AutomationProperties.LiveSettingProperty.OverrideMetadata(typeof(OdStatusText), new FrameworkPropertyMetadata(AutomationLiveSetting.Polite));

        // Added to TextBlock's own callback, which still runs first.
        TextProperty.OverrideMetadata(typeof(OdStatusText), new FrameworkPropertyMetadata(OnTextChanged));
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OdStatusText { IsLoaded: true } status && e.NewValue is string { Length: > 0 }
            && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)
            && UIElementAutomationPeer.CreatePeerForElement(status) is { } peer)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
