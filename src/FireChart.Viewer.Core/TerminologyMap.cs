namespace FireChart.Viewer.Core;

/// <summary>
/// A code of a code system: what a terminology server is asked for.
/// </summary>
/// <param name="System">The URI of the code system (<c>http://loinc.org</c>).</param>
/// <param name="Code">The code (<c>8867-4</c>).</param>
/// <example>
/// <code>
/// var key = new CodeKey("http://loinc.org", "8867-4");
/// </code>
/// </example>
public readonly record struct CodeKey(string System, string Code);

/// <summary>
/// The displays that a terminology server gave for codes, which the renderer uses when a record has no display of its own.
/// </summary>
/// <remarks>
/// The order of display of a Coding is its own <c>display</c>, else the one of this map, else the <c>text</c> of the concept, else the bare code.
/// The map travels in <see cref="ResourcePool.Terminology"/>.
/// </remarks>
/// <example>
/// <code>
/// var map = new TerminologyMap();
/// map.Add(new CodeKey("http://loinc.org", "8867-4"), "Heart rate");
/// pool.Terminology = map;
/// </code>
/// </example>
public sealed class TerminologyMap
{
    private readonly Dictionary<CodeKey, string> _displays = [];

    /// <summary>
    /// The number of codes that have a display.
    /// </summary>
    public int Count => _displays.Count;

    /// <summary>
    /// Records the display of a code; a later display of the same code replaces the earlier one.
    /// </summary>
    /// <param name="key">The code system and the code.</param>
    /// <param name="display">The display; an empty or white-space display is ignored.</param>
    public void Add(CodeKey key, string display)
    {
        if (!string.IsNullOrWhiteSpace(display))
        {
            _displays[key] = display;
        }
    }

    /// <summary>
    /// Finds the display of a code.
    /// </summary>
    /// <param name="system">The URI of the code system.</param>
    /// <param name="code">The code.</param>
    /// <returns>The display, or <see langword="null"/> when the system or the code is empty or the server gave nothing for it.</returns>
    public string? Find(string? system, string? code)
    {
        return !string.IsNullOrEmpty(system) && !string.IsNullOrEmpty(code) && _displays.TryGetValue(new CodeKey(system, code), out var display) ? display : null;
    }
}
