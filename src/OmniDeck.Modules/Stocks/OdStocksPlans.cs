namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Twelve Data's plans, cheapest first, so a listing's plan can be checked against the key's before a
/// credit is spent: a listing the key's plan does not reach is never requested.
/// </summary>
public static class OdStocksPlans
{
    private static readonly string[] Order = ["basic", "grow", "pro", "ultra", "venture", "enterprise"];

    /// <summary>Whether a key on <paramref name="keyPlan"/> reaches a listing that needs <paramref name="needed"/>; an unknown plan on either side counts as the free one.</summary>
    public static bool Covers(string? keyPlan, string? needed) => Rank(keyPlan) >= Rank(needed);

    private static int Rank(string? plan)
    {
        var index = plan is null ? -1 : Array.FindIndex(Order, name => string.Equals(name, plan.Trim(), StringComparison.OrdinalIgnoreCase));
        return Math.Max(0, index);
    }
}
