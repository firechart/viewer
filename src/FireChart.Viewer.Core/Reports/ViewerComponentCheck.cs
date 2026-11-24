namespace FireChart.Viewer.Core.Reports;

/// <summary>
/// The check of the components of a library (<c>components/{Name}.xml</c>) before they are installed: each file is parsed and checked as the Viewer
/// checks it before it shows a component (unknown blocks and attributes, variables out of scope, expressions that do not compile, cycles), with the
/// components it calls among the same files.
/// </summary>
/// <example>
/// <code>
/// var problems = ViewerComponentCheck.Check(new Dictionary&lt;string, string&gt; { ["Patient.tile"] = xml });
/// </code>
/// </example>
public static class ViewerComponentCheck
{
    /// <summary>
    /// Checks the components of a library.
    /// </summary>
    /// <param name="files">The text of each component by its name (the file <c>components/Patient.tile.xml</c> is <c>Patient.tile</c>).</param>
    /// <param name="callsElsewhere">A call of a component missing from <paramref name="files"/> is taken to be one of the vendor library (a package of a
    /// customer over it): no problem then, the Viewer checks it when it shows the page. <see langword="false"/>: every call must be among the files.</param>
    /// <param name="strings">The text of <c>locales/strings.resx</c> that goes with the components: every <c>{{%strings.Key}}</c> they name must be
    /// one of its keys. <see langword="null"/>: the texts are not checked (a package without its own file uses the texts of the vendor library).</param>
    /// <returns>The problems, empty when every component may be shown; a file of texts that is not a .resx file is one, named <c>locales/strings.resx</c>.</returns>
    /// <example>
    /// <code>
    /// foreach (var problem in ViewerComponentCheck.Check(files, strings: File.ReadAllText("locales/strings.resx")))
    /// {
    ///     Console.WriteLine($"{problem.Component}:{problem.Line}: {problem.Message}");
    /// }
    /// </code>
    /// </example>
    public static IReadOnlyList<ViewerComponentProblem> Check(IReadOnlyDictionary<string, string> files, bool callsElsewhere = false, string? strings = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        var problems = new List<ViewerComponentProblem>();
        IReadOnlySet<string>? keys = null;
        if (strings is not null)
        {
            try
            {
                keys = ReportStrings.ReadResx(strings).Keys.ToHashSet(StringComparer.Ordinal);
            }
            catch (InvalidDataException ex)
            {
                problems.Add(new ViewerComponentProblem(ReportStrings.PathOf(null), LineOf(ex.Message), ex.Message));
            }
        }

        var components = new Dictionary<string, ComponentDefinition>(StringComparer.Ordinal);
        foreach (var (name, text) in files)
        {
            try
            {
                var component = ReportXml.Parse(text);
                if (component.Name != name)
                {
                    problems.Add(new ViewerComponentProblem(name, component.Line, $"the file of {name} names its component \"{component.Name}\""));
                    continue;
                }

                components[name] = component;
            }
            catch (InvalidDataException ex)
            {
                problems.Add(new ViewerComponentProblem(name, LineOf(ex.Message), ex.Message));
            }
        }

        foreach (var component in components.Values)
        {
            problems.AddRange(ReportCheck.Check(component, components.GetValueOrDefault, keys)
                                         .Where(problem => !callsElsewhere || !problem.Message.Contains(ReportCheck.UnknownComponent, StringComparison.Ordinal))
                                         .Select(problem => new ViewerComponentProblem(problem.Component, problem.Line, problem.Message)));
        }

        return problems;
    }

    /// <summary>
    /// Reads the texts of a file of texts of a library (<c>locales/strings.resx</c>, <c>locales/strings.de.resx</c>): a .resx file of .NET, its
    /// <c>&lt;data name="…"&gt;&lt;value&gt;…&lt;/value&gt;&lt;/data&gt;</c> of strings; a data of another type (a file, an image) is left out.
    /// </summary>
    /// <param name="resx">The text of the file.</param>
    /// <returns>The texts by their keys.</returns>
    /// <exception cref="InvalidDataException">The text is not a .resx file; the message starts with the line ("12: …").</exception>
    /// <example>
    /// <code>
    /// var texts = ViewerComponentCheck.ReadTexts(File.ReadAllText("locales/strings.de.resx"));
    /// Console.WriteLine(texts["ProblemList"]);   // Problemliste
    /// </code>
    /// </example>
    public static IReadOnlyDictionary<string, string> ReadTexts(string resx)
    {
        ArgumentNullException.ThrowIfNull(resx);
        return ReportStrings.ReadResx(resx);
    }

    // "12: …": the line a parse error starts with
    private static int LineOf(string message)
    {
        return int.TryParse(message.Split(':', 2)[0], out var line) ? line : 0;
    }
}

/// <summary>
/// A problem of a component of a library.
/// </summary>
/// <param name="Component">The name of the component.</param>
/// <param name="Line">The line of its file; 0 when the problem is the file itself.</param>
/// <param name="Message">What is wrong.</param>
/// <example>
/// <code>
/// var problem = new ViewerComponentProblem("Patient.tile", 4, "&lt;Tile&gt; needs title=\"…\"");
/// </code>
/// </example>
public sealed record ViewerComponentProblem(string Component, int Line, string Message);
