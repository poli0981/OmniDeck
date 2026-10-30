namespace OmniDeck.Core.Storage;

/// <summary>One schema step: applied once, in <see cref="Version"/> order, inside a transaction.</summary>
public sealed record OdMigration(int Version, string Name, string Sql);
