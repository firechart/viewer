using System.Globalization;
using System.Text.RegularExpressions;
using Fluid.Values;

namespace FireChart.Viewer.Core;

// The texts of the templates the way i18next keeps them (locales/{language}/fhir-templates.json): the key is the English text, {{name}} is an argument,
// the plural forms are the key with _zero, _one, _two, _few, _many, _other by the rules of CLDR. The language of the interface first, then its language
// without the region, then English (its file has the singular forms), then the key itself
internal static partial class TemplateText
{
    private const string English = "en";
    private const string Count = "count";

    [GeneratedRegex(@"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    public static string Translate(IReadOnlyList<ITemplateTexts> texts, string key, IReadOnlyDictionary<string, FluidValue> arguments, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        decimal? count = arguments.TryGetValue(Count, out var value) && decimal.TryParse(value.ToStringValue(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
        var text = Find(texts, culture.Name, key, count) ?? Find(texts, culture.TwoLetterISOLanguageName, key, count) ?? Find(texts, English, key, count) ?? key;
        return Placeholder().Replace(text, match => arguments.TryGetValue(match.Groups[1].Value, out var argument) ? argument.ToStringValue() : match.Value);
    }

    private static string? Find(IReadOnlyList<ITemplateTexts> texts, string language, string key, decimal? count)
    {
        if (language.Length == 0)
        {
            return null;
        }

        var keys = count is { } n ? [$"{key}_{PluralRules.Category(language, n)}", $"{key}_other", key] : new[] { key };
        return keys.Select(candidate => texts.Select(layer => layer.Translate(language, candidate)).FirstOrDefault(found => found is not null)).FirstOrDefault(found => found is not null);
    }
}

// The plural categories of CLDR for the languages of the application (Intl.PluralRules gives the same in JavaScript); a language it does not know has "other"
internal static class PluralRules
{
    public static string Category(string language, decimal n)
    {
        var integer = n == decimal.Truncate(n);
        var i = (long)decimal.Truncate(Math.Abs(n));
        var lang = language.Split('-')[0].ToLowerInvariant();
        return lang switch
        {
            "en" or "de" or "es" or "it" or "nl" or "pt" => integer && i == 1 ? "one" : "other",
            "fr" => i is 0 or 1 ? "one" : "other",
            "ru" or "uk" when integer => (i % 10, i % 100) switch
            {
                (1, not 11) => "one",
                (>= 2 and <= 4, not (>= 12 and <= 14)) => "few",
                _ => "many"
            },
            _ => "other"
        };
    }
}
