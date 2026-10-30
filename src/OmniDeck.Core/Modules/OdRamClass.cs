namespace OmniDeck.Core.Modules;

/// <summary>The governor's priority hint: heavy modules are demoted first at equal recency.</summary>
public enum OdRamClass
{
    Light,
    Heavy,
}
