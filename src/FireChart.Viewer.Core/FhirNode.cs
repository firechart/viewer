using System.Text.RegularExpressions;
using Hl7.Fhir.ElementModel;

namespace FireChart.Viewer.Core;

// An element of a FHIR resource as templates see it
internal sealed partial class FhirNode(ITypedElement element, bool tracked = false)
{
    public const string PrimitiveTemplate = "_primitive";
    public const string ComplexTemplate = "_complex";
    public const char ModeSeparator = TemplateKey.ModeSeparator;

    // In the mode of an apply tag: the chain of modes that is in force where the tag stands
    public const string CurrentMode = "#current";

    public ITypedElement Element => element;

    // The node belongs to the resource that is rendered with the safety net (SafetyNet): reading its children is recorded
    public bool Tracked => tracked;

    public string TypeName => element.InstanceType ?? element.Name;

    // Location without indexes: Patient.contact[0].name[1] -> Patient.contact.name
    public string Path => IndexPattern().Replace(element.Location, string.Empty);

    public bool IsPrimitive => element.Value is not null;

    // The generic template of a structure (_complex, in any mode) answered: it shows nothing of the element
    public static bool IsFallbackKey(string key)
    {
        return key == ComplexTemplate || key.StartsWith(ComplexTemplate + ModeSeparator, StringComparison.Ordinal);
    }

    public static string ModeKey(string key, string mode)
    {
        return $"{key}{ModeSeparator}{mode}";
    }

    // "tile, family-first" -> [tile, family-first]: the modes of a chain, the strongest first
    public static IReadOnlyList<string> ModesOf(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode) ? [] : [.. mode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    // The chain in force under an apply tag: its own mode (where "#current" is the inherited chain), else the inherited one
    public static string? ExpandMode(string? mode, string? inherited)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return inherited;
        }

        return string.Join(',', ModesOf(mode).SelectMany(m => m == CurrentMode ? ModesOf(inherited) : [m]));
    }

    // Template keys from the most specific to the generic one; the templates of the modes (key@mode, the strongest mode first) come before the plain ones
    public IEnumerable<string> TemplateKeys(string? mode = null)
    {
        string[] keys = [Path, TypeName, IsPrimitive ? PrimitiveTemplate : ComplexTemplate];
        foreach (var name in ModesOf(mode))
        {
            foreach (var key in keys)
            {
                yield return ModeKey(key, name);
            }
        }

        foreach (var key in keys)
        {
            yield return key;
        }
    }

    // The nodes of every name asked for, and all the children for a choice, read once: a template reads the same element many times, and
    // ITypedElement makes new wrappers and looks the model up at every Children() call
    private List<ITypedElement>? _all;
    private Dictionary<string, IReadOnlyList<FhirNode>>? _byName;

    public IReadOnlyList<FhirNode> Children(string name)
    {
        _byName ??= new Dictionary<string, IReadOnlyList<FhirNode>>(8, StringComparer.Ordinal);
        if (_byName.TryGetValue(name, out var cached))
        {
            return cached;
        }

        // By name the SDK itself, which also finds a choice by its typed name (valueString)
        var found = element.Children(name).ToList();
        if (found.Count == 0)
        {
            _all ??= [.. element.Children()];
            found = [.. _all.Where(c => IsChoiceChild(c, name))];
        }

        IReadOnlyList<FhirNode> nodes = [.. found.Select(c => new FhirNode(c, tracked))];
        _byName[name] = nodes;
        return nodes;
    }

    public bool IsCollection(string name)
    {
        var children = Children(name);
        return children.Count > 0 && (children[0].Element.Definition?.IsCollection ?? false);
    }

    public string? ValueText()
    {
        return element.Value switch
        {
            null => null,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            var other => other.ToString(),
        };
    }

    // valueQuantity is the value[x] of the model, but reasonCode and reasonReference are elements of their own, not the choices of "reason".
    // Without the definition (an element the model does not know) the name has to do: the suffix is the type of the element
    private static bool IsChoiceChild(ITypedElement child, string name)
    {
        if (child.Definition is { } definition)
        {
            return definition.IsChoiceElement && definition.ElementName == name;
        }

        return IsChoiceOf(child.Name, name) && (child.InstanceType is not { Length: > 0 } type || string.Equals(child.Name[name.Length..], type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsChoiceOf(string childName, string name)
    {
        return childName.Length > name.Length && childName.StartsWith(name, StringComparison.Ordinal) && char.IsUpper(childName[name.Length]);
    }

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex IndexPattern();
}
