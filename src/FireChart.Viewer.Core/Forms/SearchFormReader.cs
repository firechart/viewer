using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// How serious a problem of a form file is.
/// </summary>
/// <example>
/// <code>
/// var errors = SearchFormReader.Read(text).Problems.Count(p =&gt; p.Level == FormProblemLevel.Error);
/// </code>
/// </example>
public enum FormProblemLevel
{
    /// <summary>A part of the file was left out or replaced by its default; the rest of the form is used.</summary>
    Warning,

    /// <summary>The file gives no form at all.</summary>
    Error
}

/// <summary>
/// A problem of a form file the lax reading worked around, or could not.
/// </summary>
/// <param name="Level">How serious it is.</param>
/// <param name="Path">Where in the file, as a path of names and indexes (<c>fields/3/control</c>); empty for the whole file.</param>
/// <param name="Message">What is wrong and what was done, in English.</param>
/// <example>
/// <code>
/// foreach (var problem in SearchFormReader.Read(text).Problems)
/// {
///     Console.WriteLine($"{problem.Level} {problem.Path}: {problem.Message}");
/// }
/// </code>
/// </example>
public sealed record FormProblem(FormProblemLevel Level, string Path, string Message);

/// <summary>
/// What <see cref="SearchFormReader.Read"/> made of a file: the form, and what it had to leave out.
/// </summary>
/// <param name="Form">The form, or <see langword="null"/> when the file gives none (see <see cref="Problems"/>).</param>
/// <param name="Problems">What was left out, replaced or refused, in the order met.</param>
/// <example>
/// <code>
/// var result = SearchFormReader.Read(text);
/// var form = result.Form ?? SearchFormGenerator.Generate("r4", "Patient");
/// </code>
/// </example>
public sealed record SearchFormReadResult(SearchFormDefinition? Form, IReadOnlyList<FormProblem> Problems);

/// <summary>
/// Reads a search form file the lax way, for the run time: a file of an older format is upgraded, and what cannot be understood is left out with a
/// problem instead of refusing the whole form.
/// </summary>
/// <remarks>
/// <para>
/// The authoring tools are strict (the JSON Schema of <see cref="ViewerJson.SchemaOf{T}"/>, the check of a designer project, <see cref="ViewerJson.Read{T}"/>
/// in the tests of the vendor library); the run time is lax, so a form written for another version of the Viewer still shows what it can:
/// </para>
/// <list type="bullet">
/// <item><description>a file of an older <see cref="SearchFormDefinition.FormatVersion"/> is upgraded step by step (<see cref="Upgrade"/>); a newer one is read as far
/// as it is understood, with a warning;</description></item>
/// <item><description>a property the model does not have is ignored;</description></item>
/// <item><description>a value that is not one of its enumeration (<c>"control": "slider"</c>) gives way to the default of the property;</description></item>
/// <item><description>a group or a field that still cannot be read (a value of a wrong type, no query name) is left out, the others stay;</description></item>
/// <item><description>only a file that is not a JSON object, or whose own properties cannot be read, gives no form (<see cref="FormProblemLevel.Error"/>).</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var result = SearchFormReader.Read("""{ "formatVersion": 1, "name": "mine", "fields": [ { "queryName": "gender", "included": false, "control": "slider" } ] }""");
/// var field = result.Form!.Fields[0];   // Hidden is true (format 1 said included: false), Control is the default
/// </code>
/// </example>
public static class SearchFormReader
{
    private static readonly JsonDocumentOptions s_document = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>
    /// Reads a search form file.
    /// </summary>
    /// <param name="json">The text of the file.</param>
    /// <returns>The form with the problems met; <see cref="SearchFormReadResult.Form"/> is <see langword="null"/> when the file gives none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var result = SearchFormReader.Read(File.ReadAllText("search-forms/r4/Patient.json"));
    /// if (result.Form is null)
    /// {
    ///     logger.LogWarning("The form cannot be read: {Problems}", string.Join("; ", result.Problems.Select(p =&gt; p.Message)));
    /// }
    /// </code>
    /// </example>
    public static SearchFormReadResult Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var problems = new List<FormProblem>();
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: s_document);
        }
        catch (JsonException exception)
        {
            problems.Add(new FormProblem(FormProblemLevel.Error, string.Empty, $"The file is not JSON: {exception.Message}"));
            return new SearchFormReadResult(null, problems);
        }

        if (node is not JsonObject root)
        {
            problems.Add(new FormProblem(FormProblemLevel.Error, string.Empty, "A search form is a JSON object."));
            return new SearchFormReadResult(null, problems);
        }

        Upgrade(root, problems);
        Prune(root, InfoOf(typeof(SearchFormDefinition)), string.Empty, problems);
        var groups = Items<SearchFormGroup>(root, "groups", problems);
        var fields = Items<SearchFormField>(root, "fields", problems);
        root.Remove("groups");
        root.Remove("fields");
        SearchFormDefinition? form;
        try
        {
            form = root.Deserialize((JsonTypeInfo<SearchFormDefinition>)InfoOf(typeof(SearchFormDefinition)));
        }
        catch (JsonException exception)
        {
            problems.Add(new FormProblem(FormProblemLevel.Error, string.Empty, $"The form cannot be read: {exception.Message}"));
            return new SearchFormReadResult(null, problems);
        }

        if (form is null)
        {
            problems.Add(new FormProblem(FormProblemLevel.Error, string.Empty, "The file is the JSON null, not a form."));
            return new SearchFormReadResult(null, problems);
        }

        form.Groups.AddRange(groups);
        form.Fields.AddRange(fields);
        return new SearchFormReadResult(form, problems);
    }

    /// <summary>
    /// Upgrades the JSON of a form of an older format to <see cref="SearchFormDefinition.CurrentFormatVersion"/>, step by step, in place. A form without
    /// a <c>formatVersion</c> is of format 1; a newer one is left as it is, with a warning.
    /// </summary>
    /// <param name="root">The JSON object of the form.</param>
    /// <param name="problems">Receives the warning of a newer format.</param>
    /// <returns><see langword="true"/> when the object was changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The steps: 1 → 2 (2026-11-13) <c>"included": false</c> of a field becomes <c>"hidden": true</c>, <c>"included": true</c> is dropped, and
    /// <c>authoredForVersion</c> (the version of the core package, <c>4.0.1</c>) becomes <c>fhirRelease</c> (<c>R4</c>). A step never loses what
    /// the author meant; a property a step does not know stays for the reading to judge.
    /// </remarks>
    /// <example>
    /// <code>
    /// var root = JsonNode.Parse("""{ "name": "old", "fields": [ { "queryName": "gender", "included": false } ] }""")!.AsObject();
    /// SearchFormReader.Upgrade(root, new List&lt;FormProblem&gt;());   // formatVersion 2, the field has "hidden": true
    /// </code>
    /// </example>
    public static bool Upgrade(JsonObject root, IList<FormProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(problems);
        var version = root["formatVersion"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 1;
        if (version > SearchFormDefinition.CurrentFormatVersion)
        {
            problems.Add(new FormProblem(FormProblemLevel.Warning, "formatVersion",
                                         $"The form is of format {version}, newer than {SearchFormDefinition.CurrentFormatVersion} of this Viewer: it is read as far as it is understood."));
            return false;
        }

        if (version == SearchFormDefinition.CurrentFormatVersion)
        {
            return false;
        }

        if (version < 2)
        {
            To2(root);
        }

        root["formatVersion"] = SearchFormDefinition.CurrentFormatVersion;
        return true;
    }

    // 1 -> 2: the field has hidden instead of included (included: false was "under more filters", which is gone); the release instead of the version
    // of the core package
    private static void To2(JsonObject root)
    {
        if (root.Remove("authoredForVersion", out var version) && version is JsonValue text && text.TryGetValue<string>(out var number)
            && ReleaseOfCoreVersion(number) is { } release && !root.ContainsKey("fhirRelease"))
        {
            root["fhirRelease"] = release;
        }

        if (root["fields"] is not JsonArray fields)
        {
            return;
        }

        foreach (var field in fields.OfType<JsonObject>())
        {
            if (field.Remove("included", out var included) && included is JsonValue flag && flag.TryGetValue<bool>(out var shown) && !shown)
            {
                field["hidden"] = true;
            }
        }
    }

    private static string? ReleaseOfCoreVersion(string version)
    {
        return version switch
        {
            _ when version.StartsWith("3.0", StringComparison.Ordinal) => "STU3",
            _ when version.StartsWith("4.0", StringComparison.Ordinal) => "R4",
            _ when version.StartsWith("4.3", StringComparison.Ordinal) => "R4B",
            _ when version.StartsWith("5.0", StringComparison.Ordinal) => "R5",
            _ => null
        };
    }

    private static JsonTypeInfo InfoOf(Type type)
    {
        return ViewerJson.Options.GetTypeInfo(type);
    }

    // The properties the model does not have are dropped, and the values that are not of their enumeration
    private static void Prune(JsonObject node, JsonTypeInfo info, string path, List<FormProblem> problems)
    {
        var properties = info.Properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var (name, value) in node.ToList())
        {
            var at = Join(path, name);
            if (!properties.TryGetValue(name, out var property))
            {
                node.Remove(name);
                problems.Add(new FormProblem(FormProblemLevel.Warning, at, $"\"{name}\" is not a property of the form here: it is ignored."));
                continue;
            }

            if (value is null)
            {
                continue;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsEnum)
            {
                if (!IsValueOf(type, value))
                {
                    node.Remove(name);
                    problems.Add(new FormProblem(FormProblemLevel.Warning, at, $"{value.ToJsonString()} is not a value of \"{name}\": its default is used."));
                }

                continue;
            }

            var inner = InfoOf(type);
            if (inner.Kind == JsonTypeInfoKind.Object && value is JsonObject child)
            {
                Prune(child, inner, at, problems);
            }
            else if (inner is { Kind: JsonTypeInfoKind.Enumerable, ElementType: { } elementType } && value is JsonArray items && InfoOf(elementType) is { Kind: JsonTypeInfoKind.Object } element)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] is JsonObject item)
                    {
                        Prune(item, element, Join(at, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), problems);
                    }
                }
            }
        }
    }

    // A group or a field that cannot be read is left out; the others stay
    private static List<T> Items<T>(JsonObject root, string name, List<FormProblem> problems)
    {
        var result = new List<T>();
        if (root[name] is not JsonArray items)
        {
            if (root[name] is not null)
            {
                problems.Add(new FormProblem(FormProblemLevel.Warning, name, $"\"{name}\" is not a list: it is ignored."));
            }

            return result;
        }

        var info = (JsonTypeInfo<T>)InfoOf(typeof(T));
        for (var i = 0; i < items.Count; i++)
        {
            var at = Join(name, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                if (items[i] is JsonObject item && item.Deserialize(info) is { } read)
                {
                    result.Add(read);
                    continue;
                }

                problems.Add(new FormProblem(FormProblemLevel.Warning, at, "Not an object: it is left out."));
            }
            catch (JsonException exception)
            {
                problems.Add(new FormProblem(FormProblemLevel.Warning, at, $"It cannot be read and is left out: {exception.Message}"));
            }
        }

        return result;
    }

    private static bool IsValueOf(Type type, JsonNode value)
    {
        if (value is not JsonValue scalar)
        {
            return false;
        }

        if (scalar.TryGetValue<int>(out _))
        {
            return true;
        }

        return scalar.TryGetValue<string>(out var text)
               && Enum.GetNames(type).Any(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase)
                                               || string.Equals(JsonNamingPolicy.CamelCase.ConvertName(n), text, StringComparison.OrdinalIgnoreCase));
    }

    private static string Join(string path, string name)
    {
        return path.Length == 0 ? name : $"{path}/{name}";
    }
}
