using System.Globalization;

namespace OmniDeck.Core.Localization;

/// <summary>The languages OmniDeck has strings for, by culture name, English (the neutral strings) first.</summary>
public static class OdLanguages
{
    public static IReadOnlyList<string> Shipped { get; } = ["en"];

    /// <summary>A language's name in that language, as language lists show it: English, Tiếng Việt, 日本語.</summary>
    public static string NativeName(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var native = culture.NativeName;
        return native.Length == 0 ? name : culture.TextInfo.ToUpper(native[0]) + native[1..];
    }
}
