using System.Globalization;
using Microsoft.JSInterop;

namespace Flox.Shared.Layout;

/// <summary>
/// The language and the direction of the page (<c>lang</c>, <c>dir</c> of <c>&lt;html&gt;</c>) follow the culture of the application: Hebrew and the
/// other right-to-left languages turn the page, Radzen and the logical properties of the CSS (margin-inline-start...) follow.
/// </summary>
public static class DocumentDirection
{
    /// <summary>
    /// <c>rtl</c> for a right-to-left culture, else <c>ltr</c>.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The value of <c>dir</c>.</returns>
    public static string Of(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";
    }

    /// <summary>
    /// Sets <c>lang</c> and <c>dir</c> of the document.
    /// </summary>
    /// <param name="js">The JavaScript runtime of the page.</param>
    /// <param name="culture">The culture of the texts.</param>
    /// <returns>A task that completes when the page has them.</returns>
    public static async Task ApplyAsync(IJSRuntime js, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(culture);
        var lang = string.IsNullOrEmpty(culture.Name) ? "en" : culture.Name;
        await js.InvokeVoidAsync("document.documentElement.setAttribute", "lang", lang);
        await js.InvokeVoidAsync("document.documentElement.setAttribute", "dir", Of(culture));
    }
}
