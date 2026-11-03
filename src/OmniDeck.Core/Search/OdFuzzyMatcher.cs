using System.Globalization;
using System.Text;

namespace OmniDeck.Core.Search;

/// <summary>
/// The command palette's matcher (docs/SPEC.md §2): every query character must appear in order;
/// matches at the start, after a word break and in a row score higher, so "cal" ranks
/// "Calculator" and "Calendar" above "Local time". Accents on Latin letters do not count, so
/// "thoi tiet" finds "Thời tiết" and "d" finds "đ".
/// </summary>
public static class OdFuzzyMatcher
{
    /// <summary>The match score, or <see langword="null"/> when <paramref name="text"/> does not match.</summary>
    public static int? Score(string query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(text);
        if (query.Length == 0)
        {
            return 0;
        }

        query = Fold(query);
        text = Fold(text);

        var score = 0;
        var position = 0;
        var previousMatch = -2;
        foreach (var q in query)
        {
            var found = IndexOfIgnoreCase(text, q, position);
            if (found < 0)
            {
                return null;
            }

            score += 1 + BoundaryBonus(text, found) + (found == previousMatch + 1 ? 3 : 0);
            previousMatch = found;
            position = found + 1;
        }

        // Shorter candidates win ties: "Timer" before "Timer settings" for "timer".
        return (score * 100) - text.Length;
    }

    /// <summary>The items that match, best first; ties keep their original order.</summary>
    public static IReadOnlyList<T> Rank<T>(string query, IEnumerable<T> items, Func<T, string> text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = query.Trim();
        return [.. items
            .Select((item, index) => (item, index, score: Score(trimmed, text(item))))
            .Where(x => x.score is not null)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Select(x => x.item)];
    }

    /// <summary>
    /// <paramref name="text"/> without the accents of its Latin letters, and with đ as d. Marks on other
    /// scripts stay: a kana's dakuten makes another sound, not an accent.
    /// </summary>
    internal static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        var latin = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                if (!latin)
                {
                    folded.Append(c);
                }

                continue;
            }

            latin = char.IsAsciiLetter(c) || c is 'đ' or 'Đ';
            folded.Append(c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => c,
            });
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int BoundaryBonus(string text, int index)
    {
        if (index == 0)
        {
            return 8;
        }

        var previous = text[index - 1];
        return !char.IsLetterOrDigit(previous) || (char.IsUpper(text[index]) && char.IsLower(previous)) ? 5 : 0;
    }

    private static int IndexOfIgnoreCase(string text, char c, int start)
    {
        var lower = char.ToLowerInvariant(c);
        for (var i = start; i < text.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) == lower)
            {
                return i;
            }
        }

        return -1;
    }
}
