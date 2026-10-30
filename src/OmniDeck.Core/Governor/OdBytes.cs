using System.Globalization;

namespace OmniDeck.Core.Governor;

/// <summary>Memory sizes as people read them: "412 MB", "4.5 GB", in the regional number format.</summary>
public static class OdBytes
{
    public static string Format(long bytes) => bytes >= OdGovernorOptions.GiB
        ? string.Format(CultureInfo.CurrentCulture, "{0:0.#} GB", bytes / (double)OdGovernorOptions.GiB)
        : string.Format(CultureInfo.CurrentCulture, "{0:0} MB", bytes / (double)OdGovernorOptions.MiB);
}
