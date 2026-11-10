using System.Net;

namespace FireChart.Viewer.Core;

/// <summary>
/// A row of a list of records: the slots a template filled, and the row the frame lays out of them.
/// </summary>
/// <remarks>
/// <para>
/// The template of the mode <see cref="Mode"/> fills named slots (<c>{% slot "title" %}…{% endslot %}</c>) and names the references of the record
/// (<c>{% slot_data patient: self.subject, encounter: self.encounter, at: self.effective %}</c>); what it writes outside the slots is dropped. Each slot
/// is sanitized on its own (<see cref="FxHtml.SanitizeTile"/>: the inline elements and blocks of the kit, no links: the whole row is the link).
/// </para>
/// <para>
/// The frame, not the template, lays the row out: the title; the second line of the subtitle, the age at the record (<see cref="TileOptions.ShowAge"/>),
/// the patient and the encounter (each only when the page does not show it above, <see cref="TileContext.Shown"/>); at the end the trailing part and the
/// status (<see cref="TileOptions.ShowStatus"/>). <see cref="Html"/> is that row, one daisyUI list row (<c>&lt;li class="fx-list-row"&gt;</c>);
/// <see cref="WithOptions"/> lays the same slots out with other options without rendering the template again.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var row = await renderer.RenderTileAsync(observationJson, context: new TileContext(["Patient/p1"], TileOptions.Default));
/// list.Add(new MarkupString(row.Html));
/// var withAge = row.WithOptions(new TileOptions(ShowAge: true));
/// </code>
/// </example>
public sealed class TileModel
{
    /// <summary>
    /// The mode of the templates of the rows: <c>tile</c>.
    /// </summary>
    public const string Mode = "tile";

    /// <summary>
    /// Creates a row of sanitized slots.
    /// </summary>
    /// <param name="title">The HTML of the title; never empty.</param>
    /// <param name="subtitle">The HTML of the record's own facts on the second line (its moment first), or empty.</param>
    /// <param name="trailing">The HTML at the end of the row (a value, badges), or empty.</param>
    /// <param name="status">The HTML of the status badge, or empty.</param>
    /// <param name="patient">The HTML that names the patient of the record on the second line, or empty (none, or shown above the list).</param>
    /// <param name="encounter">The HTML that names the encounter of the record on the second line, or empty.</param>
    /// <param name="age">The HTML of the age of the patient at the moment of the record, or empty.</param>
    /// <param name="options">The options the row is laid out with.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty.</exception>
    /// <example>
    /// <code>
    /// var row = new TileModel("Hypertension", subtitle: "2024-01-01", status: "&lt;span class=\"fx-badge\"&gt;active&lt;/span&gt;");
    /// </code>
    /// </example>
    public TileModel(string title, string subtitle = "", string trailing = "", string status = "", string patient = "", string encounter = "", string age = "",
                     TileOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        Subtitle = subtitle;
        Trailing = trailing;
        Status = status;
        Patient = patient;
        Encounter = encounter;
        Age = age;
        Options = options ?? TileOptions.Default;
        Html = Compose();
        Text = FxHtml.PlainText(Html);
    }

    /// <summary>The HTML of the title.</summary>
    public string Title { get; }

    /// <summary>The HTML of the record's own facts on the second line, or empty.</summary>
    public string Subtitle { get; }

    /// <summary>The HTML at the end of the row (a value, badges), or empty.</summary>
    public string Trailing { get; }

    /// <summary>The HTML of the status badge, or empty.</summary>
    public string Status { get; }

    /// <summary>The HTML that names the patient of the record, or empty.</summary>
    public string Patient { get; }

    /// <summary>The HTML that names the encounter of the record, or empty.</summary>
    public string Encounter { get; }

    /// <summary>The HTML of the age of the patient at the moment of the record, or empty.</summary>
    public string Age { get; }

    /// <summary>The options the row is laid out with.</summary>
    public TileOptions Options { get; }

    /// <summary>The row laid out of the slots: one <c>&lt;li class="fx-list-row"&gt;</c>.</summary>
    public string Html { get; }

    /// <summary>The same row without markup: a tooltip, a search.</summary>
    public string Text { get; }

    /// <summary>
    /// The same slots laid out with other options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The row; this one when the options are the same.</returns>
    /// <example>
    /// <code>
    /// var row = tile.WithOptions(new TileOptions(ShowAge: true, ShowStatus: false));
    /// </code>
    /// </example>
    public TileModel WithOptions(TileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options == Options ? this : new TileModel(Title, Subtitle, Trailing, Status, Patient, Encounter, Age, options);
    }

    /// <summary>
    /// A row of plain text: a title and a subtitle, encoded.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="subtitle">The subtitle, or <see langword="null"/>.</param>
    /// <returns>The row.</returns>
    /// <example>
    /// <code>
    /// var row = TileModel.Plain("p1", "Patient");
    /// </code>
    /// </example>
    public static TileModel Plain(string title, string? subtitle)
    {
        return new TileModel(Encode(title), subtitle is null ? string.Empty : Encode(subtitle));
    }

    /// <summary>
    /// The row of a record whose template failed: its title and the error.
    /// </summary>
    /// <param name="title">The title (the id or the type).</param>
    /// <param name="error">The error.</param>
    /// <returns>The row.</returns>
    /// <example>
    /// <code>
    /// var row = TileModel.Error("p1", "Template 'Patient@tile' failed");
    /// </code>
    /// </example>
    public static TileModel Error(string title, string error)
    {
        return new TileModel(Encode(title), $"<span class=\"fx-tile-error\">{Encode("Template error: " + error)}</span>");
    }

    // The second line: the own facts with the age, then the patient and the encounter, joined by " · "; the end: the trailing part and the status
    private string Compose()
    {
        var own = Join(" ", Subtitle, Options.ShowAge ? Age : string.Empty);
        var line = Join(" · ", own, Patient, Encounter);
        var end = Trailing + (Options.ShowStatus ? Status : string.Empty);
        var subtitle = line.Length == 0 ? string.Empty : $"<div class=\"fx-tile-subtitle\">{line}</div>";
        var ending = string.IsNullOrWhiteSpace(end) ? string.Empty : $"<div class=\"fx-tile-end\">{end}</div>";
        return $"<li class=\"fx-list-row\"><div class=\"fx-list-col-grow fx-tile\"><div class=\"fx-tile-main\"><div class=\"fx-tile-title\">{Title}</div>{subtitle}</div>{ending}</div></li>";
    }

    private static string Join(string separator, params ReadOnlySpan<string> parts)
    {
        var shown = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                shown.Add(part.Trim());
            }
        }

        return string.Join(separator, shown);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
