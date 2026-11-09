using System.Net;

namespace FireChart.Viewer.Core;

/// <summary>
/// A row of a list of records, ready to be placed in the page.
/// </summary>
/// <remarks>
/// The template of the mode <see cref="Mode"/> writes the row as HTML, a daisyUI list row (<c>&lt;li class="fx-list-row"&gt;</c>), and the renderer lets
/// through only what a row may have (<see cref="FxHtml.SanitizeTile"/>: the inline elements and blocks of the kit, no links: the whole row is the link).
/// </remarks>
/// <param name="Html">The sanitized HTML of the row, one <c>&lt;li&gt;</c>.</param>
/// <param name="Text">The same row without markup: a tooltip, a search.</param>
/// <example>
/// <code>
/// var row = await renderer.RenderTileAsync(patientJson);
/// list.Add(new MarkupString(row.Html));
/// </code>
/// </example>
public sealed record TileModel(string Html, string Text)
{
    /// <summary>
    /// The mode of the templates of the rows: <c>tile</c> (<c>Patient@tile</c>).
    /// </summary>
    public const string Mode = "tile";

    /// <summary>
    /// A row that shows a title and an optional subtitle: a record without a template, or a template that wrote nothing.
    /// </summary>
    /// <param name="title">The title, as text (it is encoded).</param>
    /// <param name="subtitle">The subtitle, as HTML of the kit, or <see langword="null"/>.</param>
    /// <returns>The row.</returns>
    /// <example>
    /// <code>
    /// var row = TileModel.Plain("Patient/p1", null);
    /// </code>
    /// </example>
    public static TileModel Plain(string title, string? subtitle)
    {
        return Row(Encode(title), subtitle is null ? null : Encode(subtitle));
    }

    /// <summary>
    /// A row that says what is wrong with the template, in place of the record.
    /// </summary>
    /// <param name="title">The title of the record (its type and id), as text.</param>
    /// <param name="error">The error of the template, as text.</param>
    /// <returns>The row.</returns>
    public static TileModel Error(string title, string error)
    {
        var tile = Row(Encode(title), $"<span class=\"fx-tile-error\">{Encode("Template error: " + error)}</span>");
        return new TileModel(tile.Html, $"{title} Template error: {error}");
    }

    // The HTML a template wrote: one row; a template that wrote the content only gets the <li> of the kit around it
    internal static TileModel FromTemplate(string html)
    {
        var sanitized = FxHtml.SanitizeTileWithText(html);
        var row = sanitized.Html.Trim();
        return new TileModel(row.StartsWith("<li", StringComparison.Ordinal) ? row : $"<li class=\"fx-list-row\">{row}</li>", sanitized.Text);
    }

    private static TileModel Row(string titleHtml, string? subtitleHtml)
    {
        var text = WebUtility.HtmlDecode(titleHtml) + (subtitleHtml is null ? string.Empty : " " + FxHtml.PlainText(subtitleHtml));
        var subtitle = subtitleHtml is null ? string.Empty : $"<div class=\"fx-tile-subtitle\">{subtitleHtml}</div>";
        return new TileModel($"<li class=\"fx-list-row\"><div class=\"fx-list-col-grow fx-tile\"><div class=\"fx-tile-main\"><div class=\"fx-tile-title\">{titleHtml}</div>{subtitle}</div></div></li>", text);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
