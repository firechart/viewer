namespace FireChart.Viewer.Core;

/// <summary>
/// The parts of the key of a template.
/// </summary>
/// <remarks>
/// A key is the name of a type or of a partial, optionally followed by <see cref="ModeSeparator"/> and a mode: <c>Patient</c>, <c>Patient@tile</c>,
/// <c>Coding@text</c>. The renderer prefers the template of the mode in force and falls back to the plain one.
/// </remarks>
/// <example>
/// <code>
/// var key = "Patient" + TemplateKey.ModeSeparator + TileModel.Mode;   // "Patient@tile"
/// </code>
/// </example>
public static class TemplateKey
{
    /// <summary>
    /// The character between the type and the mode: <c>@</c>.
    /// </summary>
    public const char ModeSeparator = '@';
}
