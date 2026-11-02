using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Media;

/// <summary>
/// The subtitle encodings the player offers (docs/SPEC.md §3.6): the names libVLC's <c>subsdec-encoding</c>
/// takes, for the scripts subtitle files most often come in when they are not Unicode.
/// </summary>
internal static class OdSubtitleEncodings
{
    /// <summary>libVLC decides: UTF-8 when the file reads as such, else the system's code page.</summary>
    public const string Automatic = "";

    public static IReadOnlyList<string> All { get; } =
    [
        Automatic, "UTF-8", "Windows-1252", "Windows-1250", "Windows-1251", "Windows-1253", "Windows-1254",
        "Windows-1258", "Windows-874", "Shift_JIS", "GB18030", "Big5", "EUC-KR",
    ];

    /// <summary>The encoding as the menu names it.</summary>
    public static string Title(string encoding) => encoding switch
    {
        Automatic => OdLocalization.Get("Media_EncodingAuto"),
        "UTF-8" => OdLocalization.Get("Media_EncodingUtf8"),
        "Windows-1252" => OdLocalization.Get("Media_EncodingWestern"),
        "Windows-1250" => OdLocalization.Get("Media_EncodingCentral"),
        "Windows-1251" => OdLocalization.Get("Media_EncodingCyrillic"),
        "Windows-1253" => OdLocalization.Get("Media_EncodingGreek"),
        "Windows-1254" => OdLocalization.Get("Media_EncodingTurkish"),
        "Windows-1258" => OdLocalization.Get("Media_EncodingVietnamese"),
        "Windows-874" => OdLocalization.Get("Media_EncodingThai"),
        "Shift_JIS" => OdLocalization.Get("Media_EncodingJapanese"),
        "GB18030" => OdLocalization.Get("Media_EncodingChineseSimplified"),
        "Big5" => OdLocalization.Get("Media_EncodingChineseTraditional"),
        "EUC-KR" => OdLocalization.Get("Media_EncodingKorean"),
        _ => encoding,
    };

    /// <summary>A setting a hand-edited file may carry, brought back to one the menu offers.</summary>
    public static string Known(string? encoding) => All.FirstOrDefault(known => string.Equals(known, encoding, StringComparison.OrdinalIgnoreCase)) ?? Automatic;
}
