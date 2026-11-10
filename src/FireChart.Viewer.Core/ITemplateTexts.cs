namespace FireChart.Viewer.Core;

/// <summary>
/// The translations of the static texts of the templates and of the engine, the way i18next keeps them.
/// </summary>
/// <remarks>
/// A template writes a static text as <c>{{ "Details" | t }}</c>: the key is the English text. The engine itself has a few texts of its own (the words
/// <c>Yes</c> and <c>No</c> of a boolean). A layer of a <see cref="FhirRenderer"/> that also implements this interface supplies translations; the first
/// layer that knows a key wins, so a layer placed before the vendor library overrides its translations. The language of the interface is looked up first
/// (<c>ru-RU</c>), then the language without the region (<c>ru</c>), then English, then the key itself.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyTexts : ITemplateSource, ITemplateTexts
/// {
///     public IEnumerable&lt;string&gt; Keys =&gt; [];
///     public string? Find(string key) =&gt; null;
///     public string? Translate(string culture, string key) =&gt; culture == "ru" &amp;&amp; key == "Asserted by" ? "Со слов пациента" : null;
/// }
///
/// var renderer = new FhirRenderer([new MyTexts(), new WebVendorTemplates(assets)]);
/// </code>
/// </example>
public interface ITemplateTexts
{
    /// <summary>
    /// Translates one text.
    /// </summary>
    /// <param name="culture">A culture name (<c>ru-RU</c>) or a language (<c>ru</c>).</param>
    /// <param name="key">The English text, or the English text with a plural suffix of CLDR (<c>{{count}} images_one</c>).</param>
    /// <returns>The translation, or <see langword="null"/> when this layer has none (an empty translation counts as none).</returns>
    string? Translate(string culture, string key);

    /// <summary>
    /// Loads the translations of a culture before a render, when the layer loads them on demand; <see cref="Translate"/> answers from what is loaded.
    /// </summary>
    /// <param name="culture">The culture of the interface (<c>ru-RU</c>): its language and English are loaded too.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the translations are at hand. By default nothing to load.</returns>
    /// <example>
    /// <code>
    /// await texts.PrepareAsync(CultureInfo.CurrentUICulture);
    /// </code>
    /// </example>
    ValueTask PrepareAsync(System.Globalization.CultureInfo culture, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
