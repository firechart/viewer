using System.Text;
using FireChart.Viewer.Core.Fhir.Metadata;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// Makes the search form of a resource type of a FHIR release from the metadata of the release (<see cref="FhirMetadata"/>): no server, no package,
/// no database. It is the form a list has when nobody designed one, and the start of a designed one.
/// </summary>
/// <remarks>
/// <para>
/// Every search parameter of the type is a field (not the composite and the special ones): the own ones in the form, those of all resources
/// (<c>_id</c>, <c>_lastUpdated</c>...) out of it. The order: the parameters people look for first (<see cref="Priority"/>), then the other own ones by
/// code, then those of all resources.
/// </para>
/// <para>
/// The control follows the type of the parameter: a date is a range, a number a range, a quantity a number with a unit, a reference a lookup of its
/// targets. A token of a boolean element is yes or no; a token bound to a value set is a list of its codes when the binding is required (several
/// values when the parameter takes several), else a box that suggests them and takes a typed code.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var form = SearchFormGenerator.Generate("R4", "Patient");
/// // form.Fields: identifier, name, family, given, birthdate, gender... in the form; _id, _lastUpdated... out of it
/// </code>
/// </example>
public static class SearchFormGenerator
{
    /// <summary>
    /// The name of a generated form (<see cref="SearchFormDefinition.Name"/>).
    /// </summary>
    public const string GeneratedName = "generated";

    /// <summary>
    /// The codes of the parameters a form starts with, in this order, where the type has them.
    /// </summary>
    public static IReadOnlyList<string> Priority { get; } =
    [
        "identifier", "name", "family", "given", "birthdate", "gender", "patient", "subject", "encounter", "code", "category", "type", "class", "status",
        "clinical-status", "verification-status", "intent", "date", "effective", "onset-date", "recorded-date", "authored", "authoredon", "issued",
        "period", "value-quantity", "value-concept", "performer", "requester", "author", "practitioner", "organization", "location", "url", "version",
        "title", "publisher"
    ];

    // The UCUM units a quantity field offers
    private static readonly UnitOption[] s_units =
    [
        new("kg", "kg"), new("g", "g"), new("mg", "mg"), new("[lb_av]", "lb"), new("cm", "cm"), new("m", "m"), new("[in_i]", "in"), new("Cel", "°C"),
        new("[degF]", "°F"), new("mm[Hg]", "mm[Hg]"), new("/min", "/min"), new("%", "%"), new("mmol/L", "mmol/L"), new("mg/dL", "mg/dL"), new("mL", "mL"),
        new("L", "L"), new("a", "year"), new("mo", "month"), new("wk", "week"), new("d", "day")
    ];

    /// <summary>
    /// The search form of a resource type of a release.
    /// </summary>
    /// <param name="release">The release (<c>R4</c>, <c>r4</c>, <c>STU3</c>, <c>r3</c>...).</param>
    /// <param name="resourceType">The resource type (<c>Patient</c>): a concrete resource of the release, not a datatype.</param>
    /// <returns>The form (<see cref="SearchFormDefinition.IsGenerated"/>), or <see langword="null"/> when the release has no metadata or no such resource type.</returns>
    /// <example>
    /// <code>
    /// if (SearchFormGenerator.Generate("r4", "Observation") is { } form)
    /// {
    ///     Console.WriteLine(ViewerJson.Write(form));
    /// }
    /// </code>
    /// </example>
    public static SearchFormDefinition? Generate(string? release, string resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        if (FhirMetadata.Of(ReleaseOf(release)) is not { } metadata || !metadata.IsResourceType(resourceType))
        {
            return null;
        }

        var form = new SearchFormDefinition { Name = GeneratedName, FhirRelease = metadata.Release, IsGenerated = true };
        form.Title.SetDefault("Filter");
        var priority = Priority.Select((code, index) => (code, index)).ToDictionary(p => p.code, p => p.index, StringComparer.Ordinal);
        var parameters = metadata.ParametersOf(resourceType)
                                 .Where(p => p.Type is not (SearchParameterType.Composite or SearchParameterType.Special))
                                 .OrderBy(p => p.ResourceType == resourceType ? 0 : 1)
                                 .ThenBy(p => p.ResourceType == resourceType ? priority.GetValueOrDefault(p.Code, int.MaxValue) : 0)
                                 .ThenBy(p => p.Code, StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            form.Fields.Add(FieldOf(parameter, resourceType, form.Fields.Count + 1, metadata));
        }

        return form;
    }

    /// <summary>
    /// The release of the metadata for the name of the release of a search form (<c>r3</c> is STU3, as the folders of the library name it).
    /// </summary>
    /// <param name="release">The name of the release, in any case.</param>
    /// <returns>The name of the release of <see cref="FhirMetadata"/>.</returns>
    /// <example>
    /// <code>
    /// var release = SearchFormGenerator.ReleaseOf("r3");   // STU3
    /// </code>
    /// </example>
    public static string? ReleaseOf(string? release)
    {
        return string.Equals(release?.Trim(), "r3", StringComparison.OrdinalIgnoreCase) ? "STU3" : release;
    }

    /// <summary>
    /// The label of a parameter of its code: <c>birthdate</c> is <c>Birthdate</c>, <c>general-practitioner</c> is <c>General practitioner</c>,
    /// <c>_lastUpdated</c> is <c>Last updated</c>.
    /// </summary>
    /// <param name="code">The code of the parameter.</param>
    /// <returns>The label.</returns>
    /// <example>
    /// <code>
    /// var label = SearchFormGenerator.Humanize("death-date");   // Death date
    /// </code>
    /// </example>
    public static string Humanize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var text = code.TrimStart('_');
        var builder = new StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '-' or '_')
            {
                builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(c) && char.IsLower(text[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        var words = builder.ToString().Trim().ToLowerInvariant();
        return words.Length == 0 ? code : char.ToUpperInvariant(words[0]) + words[1..];
    }

    // The control of a parameter of a type: a date is a range, a number a range, a quantity a number with a unit, a reference a lookup
    internal static SearchFieldControl ControlOf(SearchParameterType type)
    {
        return type switch
        {
            SearchParameterType.Date => SearchFieldControl.DateRange,
            SearchParameterType.Number => SearchFieldControl.NumberRange,
            SearchParameterType.Quantity => SearchFieldControl.Quantity,
            SearchParameterType.Reference => SearchFieldControl.Reference,
            _ => SearchFieldControl.Text
        };
    }

    private static SearchFormField FieldOf(SearchParameterMetadata parameter, string resourceType, int order, FhirReleaseMetadata metadata)
    {
        var field = new SearchFormField
        {
            QueryName = parameter.Code,
            Origin = SearchFieldOrigin.Package,
            ParameterType = parameter.Type,
            Hidden = parameter.ResourceType != resourceType,
            Order = order,
            MultipleOr = parameter.MultipleOr ?? false,
            MultipleAnd = parameter.MultipleAnd ?? false,
            Control = ControlOf(parameter.Type),
            Source = new FieldSource { ParameterUrl = parameter.Url, Expression = parameter.Expression }
        };
        field.Label.SetDefault(Humanize(parameter.Code));
        if (parameter.Hint is { Length: > 0 } hint)
        {
            field.Hint.SetDefault(hint);
        }

        field.Comparators.AddRange(parameter.Comparators);
        field.Modifiers.AddRange(parameter.Modifiers);
        field.Source.Base.AddRange(parameter.Bases);
        switch (parameter.Type)
        {
            case SearchParameterType.Quantity:
                foreach (var option in s_units)
                {
                    var unit = new QuantityUnit { Code = option.Code };
                    unit.Label.SetDefault(option.Label);
                    field.Units.Add(unit);
                }

                break;
            case SearchParameterType.Reference:
                field.Lookup = new ReferenceLookup();
                field.Lookup.TargetTypes.AddRange(parameter.Targets);
                break;
            case SearchParameterType.Token when parameter.ElementTypes is ["boolean"]:
                field.Control = SearchFieldControl.Boolean;
                break;
            case SearchParameterType.Token when parameter.Path is { } path && metadata.BindingOf(path) is { } binding:
                field.ValueSetUrl = binding.ValueSetUrl;
                field.ValueSetVersion = binding.ValueSetVersion;
                // Only a required binding is a closed, usually short list; the others (LOINC, SNOMED) are suggested as the user types and may be typed in
                if (binding.Strength == BindingStrength.Required)
                {
                    field.Control = field.MultipleOr ? SearchFieldControl.MultiSelect : SearchFieldControl.Select;
                }
                else
                {
                    field.Control = SearchFieldControl.Autocomplete;
                    field.AllowFreeText = true;
                }

                break;
        }

        return field;
    }

    private sealed record UnitOption(string Code, string Label);
}
