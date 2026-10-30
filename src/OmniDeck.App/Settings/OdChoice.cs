namespace OmniDeck.App.Settings;

/// <summary>One entry of a settings choice: the value stored and the text shown.</summary>
public sealed record OdChoice<T>(T Value, string Title);
