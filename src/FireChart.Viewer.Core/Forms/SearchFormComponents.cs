using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using FireChart.Viewer.Core.Fhir.Metadata;
using FireChart.Viewer.Core.Reports;

namespace FireChart.Viewer.Core.Forms;

// The search form of a resource type is the component {Type}.search of the library: one <SearchForm> of <Filter>s and <Group>s, one file for every FHIR
// release. A filter names a search parameter (or a chain, or a parameter of a server) and changes only what the generated field of the parameter does
// not do as wanted; a parameter the release does not have is left out, so is a filter whose releases="…" does not name the release. The parameters the
// form does not name are in it as hidden fields (a value in the address of a list is still shown in the dialog). The texts are keys of the resources
internal static partial class SearchFormComponents
{
    public const string Mode = "search";
    public const string SearchForm = "SearchForm";
    public const string Group = "Group";
    public const string Filter = "Filter";

    // The elements of a search form and their attributes
    public static readonly FrozenDictionary<string, FrozenSet<string>> Elements = new Dictionary<string, FrozenSet<string>>
    {
        [SearchForm] = new[] { "title", "description" }.ToFrozenSet(),
        [Group] = new[] { "label", "collapsed", "hidden" }.ToFrozenSet(),
        [Filter] = new[]
        {
            "param", "label", "hint", "placeholder", "control", "type", "custom", "releases", "hidden", "required", "default", "valueSet", "valueSetVersion",
            "freeText", "or", "and", "comparators", "comparator", "modifiers", "modifier", "minLength", "maxLength", "min", "max", "pattern", "units",
            "unit", "targets", "lookup", "span"
        }.ToFrozenSet()
    }.ToFrozenDictionary();

    // The releases a filter may name, as the releases of the Viewer are named in lower case
    public static readonly FrozenSet<string> Releases = new[] { "r3", "r4", "r4b", "r5" }.ToFrozenSet();

    private static readonly FrozenSet<string> s_flags = new[] { "custom", "hidden", "required", "freeText", "or", "and", "collapsed" }.ToFrozenSet();
    private static readonly FrozenSet<string> s_numbers = new[] { "minLength", "maxLength", "span" }.ToFrozenSet();

    public static bool IsSearchForm(string componentName)
    {
        return componentName.EndsWith("." + Mode, StringComparison.Ordinal);
    }

    public static string NameOf(string resourceType)
    {
        return $"{resourceType}.{Mode}";
    }

    // The problems of a search form; keys: the keys of locales/strings.resx (null: not checked)
    public static IReadOnlyList<ReportProblem> Check(ComponentDefinition component, IReadOnlySet<string>? keys)
    {
        var problems = new List<ReportProblem>();
        void Add(int line, string message)
        {
            problems.Add(new ReportProblem(component.Name, line, message));
        }

        if (component.Params.Length > 0 || component.Slots.Length > 0 || component.Data.Length > 0)
        {
            Add(component.Line, "a search form takes no <Param>, <Slot> or <Data>");
        }

        var resourceType = component.Name[..^(Mode.Length + 1)];
        var known = FhirMetadata.Releases.Select(FhirMetadata.Of).OfType<FhirReleaseMetadata>().Where(metadata => metadata.IsResourceType(resourceType)).ToList();
        if (known.Count == 0)
        {
            Add(component.Line, $"{resourceType} is not a resource type of any FHIR release");
        }

        if (component.Body is not [{ Name: SearchForm } form])
        {
            Add(component.Line, $"a search form is one <{SearchForm}>");
            return problems;
        }

        Attributes(form);
        var parameters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in form.Children)
        {
            if (child.Name == Group)
            {
                Attributes(child);
                foreach (var filter in child.Children)
                {
                    Filtered(filter, $"<{Group}> holds only <{Filter}>");
                }
            }
            else
            {
                Filtered(child, $"<{SearchForm}> holds only <{Filter}> and <{Group}>");
            }
        }

        return problems;

        void Filtered(ReportNode node, string otherwise)
        {
            if (node.Name != Filter)
            {
                Add(node.Line, otherwise);
                return;
            }

            Attributes(node);
            if (node.Attribute("param")?.Source is not { Length: > 0 } param)
            {
                Add(node.Line, $"<{Filter}> needs param=\"…\"");
                return;
            }

            if (!parameters.Add(param))
            {
                Add(node.Line, $"the parameter {param} is a filter twice");
            }

            if (!ParamRegex().IsMatch(param))
            {
                Add(node.Line, $"\"{param}\" is not a search parameter, a chain (subject:Patient.name) or a parameter of a server");
            }
            else if (node.Attribute("custom")?.Source != "true" && known.Count > 0 && known.All(metadata => TypeOf(metadata, resourceType, param) is null))
            {
                Add(node.Line, $"{resourceType} has no search parameter {param} in any FHIR release; custom=\"true\" for a parameter of a server");
            }
        }

        void Attributes(ReportNode node)
        {
            if (node.Children.Length > 0 && node.Name == Filter)
            {
                Add(node.Line, $"<{Filter}> takes no content");
            }

            if (node.Name == SearchForm && node.Children.Any(child => child.Name == ReportNode.TextName))
            {
                Add(node.Line, $"<{SearchForm}> holds no text");
            }

            var allowed = Elements[node.Name];
            foreach (var (name, value) in node.Attributes)
            {
                if (!allowed.Contains(name))
                {
                    Add(node.Line, $"<{node.Name}> has no attribute {name} (it takes {string.Join(", ", allowed)})");
                    continue;
                }

                if (value.Parts.Any(part => part is ReportExpression) || value.Strings.Any(text => text.Arguments.Length > 0))
                {
                    Add(node.Line, $"{name}=\"{value.Source}\": a search form has no data, only texts ({{{{%strings.Key}}}})");
                }

                foreach (var text in value.Strings.Where(text => keys is not null && !keys.Contains(text.Key)))
                {
                    Add(node.Line, $"%strings.{text.Key} is not a key of locales/strings.resx");
                }

                if (ProblemOf(name, value.Source) is { } problem)
                {
                    Add(node.Line, problem);
                }
            }
        }
    }

    // What is wrong with the value of an attribute of a closed set, a flag or a number
    private static string? ProblemOf(string name, string value)
    {
        if (s_flags.Contains(name))
        {
            return value is "true" or "false" ? null : $"{name}=\"{value}\" is true or false";
        }

        if (s_numbers.Contains(name))
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 && (name != "span" || number <= 12)
                ? null
                : $"{name}=\"{value}\" is a whole number{(name == "span" ? " from 1 to 12" : " above 0")}";
        }

        return name switch
        {
            "control" when !Enum.TryParse<SearchFieldControl>(value, true, out _) || !char.IsLower(value[0]) =>
                $"the control \"{value}\" is none of {string.Join(", ", Enum.GetNames<SearchFieldControl>().Select(Camel))}",
            "type" when !Enum.TryParse<SearchParameterType>(value, true, out _) || !char.IsLower(value[0]) =>
                $"the type \"{value}\" is none of {string.Join(", ", Enum.GetNames<SearchParameterType>().Select(Camel))}",
            "releases" when Words(value) is var releases && (releases.Count == 0 || releases.Any(release => !Releases.Contains(release))) =>
                $"releases=\"{value}\" names the releases among {string.Join(", ", Releases.Order())}",
            _ => null
        };
    }

    // The form of a release: the filters of the component over the generated form of the type; null when the release has no such resource type
    public static SearchFormDefinition? Read(ComponentDefinition component, string release, ReportStrings strings, CultureInfo culture)
    {
        var resourceType = component.Name[..^(Mode.Length + 1)];
        if (SearchFormGenerator.Generate(release, resourceType) is not { } generated || FhirMetadata.Of(SearchFormGenerator.ReleaseOf(release)) is not { } metadata)
        {
            return null;
        }

        var form = new SearchFormDefinition { Name = component.Name, FhirRelease = generated.FhirRelease };
        var root = component.Body.FirstOrDefault(node => node.Name == SearchForm);
        string? Text(ReportNode node, string name)
        {
            return node.Attribute(name) is { } value
                ? string.Concat(value.Parts.Select(part => part switch
                {
                    ReportLiteral literal => literal.Text,
                    ReportString text => strings.Format(text.Key, [], culture),
                    _ => string.Empty
                }))
                : null;
        }

        form.Title.SetDefault((root is null ? null : Text(root, "title")) ?? generated.Title.Resolve(null));
        if (root is not null && Text(root, "description") is { Length: > 0 } description)
        {
            form.Description.SetDefault(description);
        }

        var generatedFields = generated.Fields.ToDictionary(field => field.QueryName, StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        void Add(ReportNode node, string? groupCode)
        {
            var param = node.Attribute("param")?.Source ?? string.Empty;
            if (node.Attribute("releases")?.Source is { } releases && !Words(releases).Contains(release.ToLowerInvariant()))
            {
                return;
            }

            var custom = node.Attribute("custom")?.Source == "true";
            SearchFormField field;
            if (!custom && generatedFields.TryGetValue(param, out var known))
            {
                field = known;
            }
            else if (!custom && TypeOf(metadata, resourceType, param) is { } chained)
            {
                field = new SearchFormField { QueryName = param, Origin = SearchFieldOrigin.Chained, ParameterType = chained, Control = SearchFormGenerator.ControlOf(chained) };
                field.Label.SetDefault(SearchFormGenerator.Humanize(param.Split('.')[^1]));
            }
            else if (custom)
            {
                var type = node.Attribute("type")?.Source is { } typeName && Enum.TryParse<SearchParameterType>(typeName, true, out var given) ? given : SearchParameterType.String;
                field = new SearchFormField { QueryName = param, Origin = SearchFieldOrigin.Custom, ParameterType = type, Control = SearchFormGenerator.ControlOf(type) };
                field.Label.SetDefault(SearchFormGenerator.Humanize(param));
            }
            else
            {
                return;
            }

            named.Add(param);
            field.GroupCode = groupCode;
            field.Order = ++order;
            field.Hidden = Flag(node, "hidden") ?? false;
            Apply(node, field, Text);
            form.Fields.Add(field);
        }

        foreach (var child in root?.Children ?? [])
        {
            if (child.Name == Group)
            {
                var group = new SearchFormGroup
                {
                    Code = $"group{form.Groups.Count + 1}", Order = form.Groups.Count + 1, CollapsedByDefault = Flag(child, "collapsed") ?? false,
                    Hidden = Flag(child, "hidden") ?? false
                };
                if (Text(child, "label") is { Length: > 0 } label)
                {
                    group.Caption.SetDefault(label);
                }

                form.Groups.Add(group);
                foreach (var filter in child.Children.Where(node => node.Name == Filter))
                {
                    Add(filter, group.Code);
                }
            }
            else if (child.Name == Filter)
            {
                Add(child, null);
            }
        }

        foreach (var field in generated.Fields.Where(field => !named.Contains(field.QueryName)))
        {
            field.Hidden = true;
            field.Order = ++order;
            form.Fields.Add(field);
        }

        return form;
    }

    private static void Apply(ReportNode node, SearchFormField field, Func<ReportNode, string, string?> text)
    {
        string? Plain(string name)
        {
            return node.Attribute(name)?.Source is { Length: > 0 } value ? value : null;
        }

        void Localized(string name, LocalizedString target)
        {
            if (text(node, name) is { } value)
            {
                target.Clear();
                target.SetDefault(value);
            }
        }

        Localized("label", field.Label);
        Localized("hint", field.Hint);
        Localized("placeholder", field.Placeholder);
        if (Plain("control") is { } control && Enum.TryParse<SearchFieldControl>(control, true, out var parsed))
        {
            field.Control = parsed;
        }

        if (Plain("type") is { } type && Enum.TryParse<SearchParameterType>(type, true, out var parameterType))
        {
            field.ParameterType = parameterType;
        }

        field.Required = Flag(node, "required") ?? field.Required;
        field.DefaultValue = node.Attribute("default")?.Source ?? field.DefaultValue;
        field.ValueSetUrl = Plain("valueSet") ?? field.ValueSetUrl;
        field.ValueSetVersion = Plain("valueSetVersion") ?? field.ValueSetVersion;
        field.AllowFreeText = Flag(node, "freeText") ?? field.AllowFreeText;
        field.MultipleOr = Flag(node, "or") ?? field.MultipleOr;
        field.MultipleAnd = Flag(node, "and") ?? field.MultipleAnd;
        field.DefaultComparator = Plain("comparator") ?? field.DefaultComparator;
        field.DefaultModifier = Plain("modifier") ?? field.DefaultModifier;
        field.MinLength = Number(node, "minLength") ?? field.MinLength;
        field.MaxLength = Number(node, "maxLength") ?? field.MaxLength;
        field.Span = Number(node, "span") ?? field.Span;
        field.Min = Plain("min") ?? field.Min;
        field.Max = Plain("max") ?? field.Max;
        field.Pattern = Plain("pattern") ?? field.Pattern;
        field.DefaultUnit = Plain("unit") ?? field.DefaultUnit;
        if (Plain("comparators") is { } comparators)
        {
            field.Comparators.Clear();
            field.Comparators.AddRange(Words(comparators));
        }

        if (Plain("modifiers") is { } modifiers)
        {
            field.Modifiers.Clear();
            field.Modifiers.AddRange(Words(modifiers));
        }

        if (Plain("units") is { } units)
        {
            field.Units.Clear();
            field.Units.AddRange(Words(units).Select(code => new QuantityUnit { Code = code, Label = { [LocalizedString.DefaultCulture] = code } }));
        }

        if (Plain("targets") is { } targets || Plain("lookup") is not null || field.Control == SearchFieldControl.Reference)
        {
            field.Lookup ??= new ReferenceLookup();
            if (Plain("targets") is { } chosen)
            {
                field.Lookup.TargetTypes.Clear();
                field.Lookup.TargetTypes.AddRange(Words(chosen));
            }

            field.Lookup.LookupParameter = Plain("lookup") ?? field.Lookup.LookupParameter;
        }
    }

    // The type of the value of a parameter or of the last parameter of a chain (subject:Patient.name, patient.birthdate); null when the release has no such
    // parameter
    public static SearchParameterType? TypeOf(FhirReleaseMetadata metadata, string resourceType, string param)
    {
        var links = param.Split('.');
        IReadOnlyList<string> types = [resourceType];
        SearchParameterType? type = null;
        foreach (var link in links)
        {
            var parts = link.Split(':', 2);
            var parameter = types.Select(candidate => metadata.ParametersOf(candidate).FirstOrDefault(p => p.Code == parts[0])).OfType<SearchParameterMetadata>().FirstOrDefault();
            if (parameter is null)
            {
                return null;
            }

            type = parameter.Type;
            types = parts.Length == 2 ? [parts[1]] : parameter.Targets;
        }

        return type;
    }

    private static bool? Flag(ReportNode node, string name)
    {
        return node.Attribute(name)?.Source switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };
    }

    private static int? Number(ReportNode node, string name)
    {
        return int.TryParse(node.Attribute(name)?.Source, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static List<string> Words(string value)
    {
        return [.. value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static string Camel(string name)
    {
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    // A code, a chain of codes (each may name its type: subject:Patient), the modifiers of the last one stay in the form
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*(:[A-Za-z]+)?(\\.[A-Za-z_][A-Za-z0-9_-]*(:[A-Za-z]+)?)*$")]
    private static partial Regex ParamRegex();
}
