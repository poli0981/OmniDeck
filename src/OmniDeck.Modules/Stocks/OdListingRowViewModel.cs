using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Stocks;

/// <summary>A search result: the listing, where it trades, and whether the key's plan reaches it.</summary>
public sealed class OdListingRowViewModel(OdListing listing, bool covered, bool added)
{
    public OdListing Listing { get; } = listing;

    public string Title { get; } = OdLocalization.Format("Stocks_ResultTitle", listing.Symbol, listing.Name);

    public string Detail { get; } = OdLocalization.Format("Stocks_ResultDetail", listing.Exchange ?? listing.Mic ?? "", listing.Country ?? "");

    /// <summary>The plan the listing needs when the key's does not reach it; empty otherwise.</summary>
    public string PlanNote { get; } = covered ? "" : OdLocalization.Format("Stocks_NeedsPlan", listing.Plan ?? "");

    /// <summary>Whether it can go on the watchlist: the plan reaches it and it is not there yet.</summary>
    public bool CanAdd { get; } = covered && !added;

    /// <summary>What screen readers hear for its Add button.</summary>
    public string AddName { get; } = OdLocalization.Format("Stocks_AddNamed", listing.Symbol, listing.Name);

    public override string ToString() => PlanNote.Length == 0 ? Title : OdLocalization.Format("Stocks_RowNote", Title, PlanNote);
}
