namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// A text in several languages: culture tag (BCP-47, case-insensitive) to text. <see cref="DefaultCulture"/> is the default and the last fallback.
/// </summary>
/// <remarks>
/// It is stored inside the form that owns it and written as a JSON object whose keys keep their case (<c>"ru-RU"</c> stays <c>"ru-RU"</c>).
/// </remarks>
/// <example>
/// <code>
/// var title = new LocalizedString("Vital signs") { ["ru"] = "Показатели жизнедеятельности" };
/// title.Resolve("ru-RU");   // "Показатели жизнедеятельности"
/// title.Resolve("ja");      // "Vital signs"
/// </code>
/// </example>
public sealed class LocalizedString : Dictionary<string, string>
{
    /// <summary>
    /// The culture of the default text: <c>en-US</c>.
    /// </summary>
    public const string DefaultCulture = "en-US";

    /// <summary>
    /// Creates an empty text.
    /// </summary>
    public LocalizedString() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Creates a text with its default (<see cref="DefaultCulture"/>) value.
    /// </summary>
    /// <param name="defaultText">The text in <see cref="DefaultCulture"/>.</param>
    public LocalizedString(string defaultText) : this()
    {
        this[DefaultCulture] = defaultText;
    }

    /// <summary>
    /// Whether the default text is set and not white space.
    /// </summary>
    public bool HasDefault
    {
        get
        {
            return TryGetValue(DefaultCulture, out var text) && !string.IsNullOrWhiteSpace(text);
        }
    }

    /// <summary>
    /// A text with only its default value.
    /// </summary>
    /// <param name="defaultText">The text in <see cref="DefaultCulture"/>.</param>
    public static implicit operator LocalizedString(string defaultText)
    {
        return FromString(defaultText);
    }

    /// <summary>
    /// A text with only its default value.
    /// </summary>
    /// <param name="defaultText">The text in <see cref="DefaultCulture"/>.</param>
    /// <returns>The text.</returns>
    public static LocalizedString FromString(string defaultText)
    {
        return new LocalizedString(defaultText);
    }

    /// <summary>
    /// Sets the default text.
    /// </summary>
    /// <param name="value">The text in <see cref="DefaultCulture"/>.</param>
    public void SetDefault(string value)
    {
        this[DefaultCulture] = value;
    }

    /// <summary>
    /// The text for a culture: the exact culture, then its language (<c>ru-RU</c> to <c>ru</c>), then another region of the same language, then the default.
    /// </summary>
    /// <param name="culture">A culture tag, or <see langword="null"/> for the default.</param>
    /// <returns>The text, or an empty string when there is none at all. Empty and white-space texts do not count.</returns>
    public string Resolve(string? culture)
    {
        if (!string.IsNullOrWhiteSpace(culture))
        {
            if (TryGetText(culture, out var exact))
            {
                return exact;
            }

            var language = LanguageOf(culture);
            if (TryGetText(language, out var byLanguage))
            {
                return byLanguage;
            }

            foreach ((var key, var text) in this)
            {
                if (!string.IsNullOrWhiteSpace(text) && string.Equals(LanguageOf(key), language, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
        }

        return TryGetText(DefaultCulture, out var fallback) ? fallback : string.Empty;
    }

    private bool TryGetText(string key, out string text)
    {
        if (TryGetValue(key, out var found) && !string.IsNullOrWhiteSpace(found))
        {
            text = found;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static string LanguageOf(string culture)
    {
        var dash = culture.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? culture[..dash] : culture;
    }
}
