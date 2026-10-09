using System.Text;
using System.Text.RegularExpressions;

namespace Say.Services;

public static partial class VocabularyCorrector
{
    public const int MaxPromptChars = 400;
    private const int MaxEntryLength = 64;
    private const int MaxEntries = 200;

    /// <summary>
    /// Splits a user-provided blob (one per line and/or comma-separated) into a clean,
    /// deduplicated vocabulary list.
    /// </summary>
    public static List<string> Parse(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var raw in text.Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = raw.Trim();
            if (entry.Length == 0 || entry.Length > MaxEntryLength)
            {
                continue;
            }

            if (result.Any(existing => string.Equals(existing, entry, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(entry);
            if (result.Count >= MaxEntries)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Builds the initial prompt that biases whisper toward the vocabulary spellings.</summary>
    public static string? BuildPrompt(IReadOnlyList<string> vocabulary)
    {
        if (vocabulary.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder("Glossary: ");
        var first = true;
        foreach (var entry in vocabulary)
        {
            var addition = (first ? string.Empty : ", ") + entry;
            if (sb.Length + addition.Length + 1 > MaxPromptChars)
            {
                break;
            }

            sb.Append(addition);
            first = false;
        }

        if (first)
        {
            return null;
        }

        sb.Append('.');
        return sb.ToString();
    }

    /// <summary>
    /// Fixes near-misses in the transcribed text: exact words get the canonical casing,
    /// close matches (small edit distance) are replaced, punctuation is preserved.
    /// </summary>
    public static string Apply(string text, IReadOnlyList<string> vocabulary)
    {
        if (string.IsNullOrEmpty(text) || vocabulary.Count == 0)
        {
            return text;
        }

        var result = text;

        foreach (var entry in vocabulary.Where(e => e.Contains(' ')))
        {
            var pattern = $@"\b{Regex.Escape(entry)}\b";
            result = Regex.Replace(
                result, pattern, entry,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }

        var singleWords = vocabulary.Where(e => !e.Contains(' ')).ToList();
        if (singleWords.Count == 0)
        {
            return result;
        }

        var parts = TokenRegex().Split(result);
        var changed = false;

        for (var i = 0; i < parts.Length; i++)
        {
            var token = parts[i];
            if (token.Length < 2 || !token.All(char.IsLetter))
            {
                continue;
            }

            var canonical = FindCanonical(token, singleWords);
            if (canonical is not null && !string.Equals(canonical, token, StringComparison.Ordinal))
            {
                parts[i] = canonical;
                changed = true;
            }
        }

        return changed ? string.Concat(parts) : result;
    }

    private static string? FindCanonical(string token, IReadOnlyList<string> vocabulary)
    {
        foreach (var entry in vocabulary)
        {
            if (string.Equals(entry, token, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        if (token.Length < 3)
        {
            return null;
        }

        var maxDistance = token.Length >= 6 ? 2 : 1;
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var entry in vocabulary)
        {
            if (Math.Abs(entry.Length - token.Length) > maxDistance)
            {
                continue;
            }

            var distance = Levenshtein(token, entry, maxDistance);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entry;
            }
        }

        return bestDistance <= maxDistance ? best : null;
    }

    private static int Levenshtein(string a, string b, int maxDistance)
    {
        var upperA = a.ToUpperInvariant();
        var upperB = b.ToUpperInvariant();

        var previous = new int[upperB.Length + 1];
        var current = new int[upperB.Length + 1];

        for (var j = 0; j <= upperB.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= upperA.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];

            for (var j = 1; j <= upperB.Length; j++)
            {
                var cost = upperA[i - 1] == upperB[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > maxDistance)
            {
                return maxDistance + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[upperB.Length];
    }

    [GeneratedRegex(@"(\P{L}+)")]
    private static partial Regex TokenRegex();
}
