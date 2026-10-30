namespace OmniDeck.Core.Storage;

/// <summary>The outcome of <see cref="OdJsonFile.Read{T}"/>.</summary>
public enum OdJsonReadStatus
{
    /// <summary>The file was read.</summary>
    Loaded,

    /// <summary>There was no file.</summary>
    Missing,

    /// <summary>The file could not be parsed and was renamed to <c>*.corrupt-&lt;timestamp&gt;.json</c>.</summary>
    Quarantined,
}
