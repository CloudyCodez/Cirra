using System.Globalization;
using System.Text;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App.Services;

internal static class CommandPaletteSearch
{
    public static IReadOnlyList<CommandPaletteItemViewModel> Find(
        IEnumerable<CommandPaletteItemViewModel> items, string? query, int limit = 60)
    {
        var input = (query ?? string.Empty).Trim();
        var scope = string.Empty;
        var separator = input.IndexOf(':');
        if (separator >= 0)
        {
            var candidate = input[..separator].ToLowerInvariant();
            if (candidate is "games" or "commands" or "themes" or "views")
            {
                scope = candidate;
                input = input[(separator + 1)..].Trim();
            }
        }

        var normalized = Normalize(input);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return items.Where(item => scope switch
            {
                "games" => item.IsGame,
                "themes" => item.Category == "Theme",
                "views" => item.Category == "Saved view",
                "commands" => !item.IsGame && item.Category is not ("Theme" or "Saved view"),
                _ => true
            })
            .Select(item => new { Item = item, Title = Normalize(item.Title), Search = Normalize(item.SearchText) })
            .Where(match => tokens.All(token => match.Search.Contains(token, StringComparison.Ordinal)))
            .OrderBy(match => tokens.Length == 0
                ? match.Item.Game?.LastPlayedOn is not null ? 0 : match.Item.Category == "Saved view" ? 1 : match.Item.IsGame ? 3 : 2
                : match.Title == normalized ? 0 : match.Title.StartsWith(normalized, StringComparison.Ordinal) ? 1 : match.Title.Contains(normalized, StringComparison.Ordinal) ? 2 : 3)
            .ThenByDescending(match => match.Item.Game?.LastPlayedOn)
            .ThenBy(match => match.Item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Max(0, limit))
            .Select(match => match.Item).ToArray();
    }

    // Search tolerates accents, punctuation and repeated whitespace, while the
    // original titles remain untouched for display and persistence.
    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) result.Append(char.ToLowerInvariant(character));
            else if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
        }
        return result.ToString().Trim();
    }
}
