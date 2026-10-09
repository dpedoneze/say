using System.Text.RegularExpressions;

namespace Say.Services;

public static partial class TextNormalizer
{
    public static string Normalize(string? text, bool appendTrailingSpace)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(text, " ").Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (appendTrailingSpace && !normalized.EndsWith(' '))
        {
            normalized += " ";
        }

        return normalized;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
