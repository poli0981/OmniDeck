using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// What screen readers get from a chart: its summary as the name, a hint for the keys, and the values
/// at the cursor as a read-only value that changes as the cursor moves.
/// </summary>
public sealed class OdChartAutomationPeer(OdChartBase owner) : FrameworkElementAutomationPeer(owner), IValueProvider
{
    public bool IsReadOnly => true;

    public string Value => owner.Readout;

    public void SetValue(string value) => throw new ElementNotEnabledException();

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

    /// <summary>Tells screen readers that the values at the cursor changed.</summary>
    internal void RaiseValueChanged(string oldValue, string newValue) =>
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, newValue);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetLocalizedControlTypeCore() => OdLocalization.Get("Chart_Type");

    protected override string GetClassNameCore() => owner.GetType().Name;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? owner.Summary : name;
    }

    protected override string GetHelpTextCore()
    {
        var help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? OdLocalization.Get("Chart_KeyboardHint") : help;
    }
}
