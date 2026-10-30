namespace OmniDeck.Modules.Calculator;

/// <summary>What the calculator remembers between sessions; its history does not outlive the session.</summary>
public sealed record OdCalculatorSettings
{
    public bool Scientific { get; init; }

    /// <summary>Trigonometry in degrees (the default) or radians.</summary>
    public bool Degrees { get; init; } = true;
}
