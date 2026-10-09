using Say.Models;

namespace Say.Services;

public static class LanguageSelection
{
    public static IReadOnlyList<string> Candidates(AppSettings settings)
    {
        var list = new List<string>();
        foreach (var code in settings.Languages)
        {
            if (string.IsNullOrWhiteSpace(code)
                || string.Equals(code, "auto", StringComparison.OrdinalIgnoreCase)
                || list.Any(existing => string.Equals(existing, code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(code);
        }

        return list;
    }

    public static string? FixedLanguage(AppSettings settings)
    {
        var candidates = Candidates(settings);
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static bool NeedsRestrictedDetection(AppSettings settings) =>
        Candidates(settings).Count >= 2;
}
