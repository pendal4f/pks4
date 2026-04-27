using System.Text;
using pks4.Models;

namespace pks4.Utilities;

public static class SearchTokens
{
    public static string CitySearchText(City city)
    {
        var parts = new List<string> { city.Name };

        foreach (var alias in CityAliases(city.Name))
            parts.Add(alias);

        return Normalize(string.Join(' ', parts));
    }

    public static bool MatchesQuery(string haystackNormalized, string? query)
    {
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return true;

        foreach (var token in tokens)
        {
            if (!haystackNormalized.Contains(token, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);

        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '‑' or '—' or '–' or '_' or '.' or ',')
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(ch);
        }

        return CollapseSpaces(sb.ToString());
    }

    private static List<string> Tokenize(string? query)
    {
        var normalized = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string CollapseSpaces(string text)
        => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IEnumerable<string> CityAliases(string cityName)
    {
        return cityName switch
        {
            "Москва" => new[] { "мск" },
            "Санкт‑Петербург" => new[] { "спб", "питер", "санкт петербург", "петроград", "ленинград" },
            "Казань" => new[] { "кзн" },
            "Нижний Новгород" => new[] { "нн", "н новгород", "нижний" },
            "Калининград" => new[] { "кгд", "калик", "кёнигсберг" , "кенигсберг"},
            "Владивосток" => new[] { "владик", "влд" },
            "Иркутск" => new[] { "ирк" },
            "Мурманск" => new[] { "мурм" },
            _ => Array.Empty<string>()
        };
    }
}
