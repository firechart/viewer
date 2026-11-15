using System.Collections.Frozen;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Core.Fhir.Metadata;

/// <summary>
/// What the search forms need of a FHIR release besides the Firely SDK: the concrete resource types, the value sets the elements of the resources and
/// datatypes are bound to, and the search parameters with all a form needs of them (the Firely SDK lacks the parameters of all resources and the hints of
/// the shared ones, the comparators, the modifiers, the chains). Generated from the core package of the release (the classes
/// <c>*.g.cs</c> of this folder, not to be edited), so nothing is read at run time.
/// </summary>
/// <example>
/// <code>
/// var r4 = FhirMetadata.Of("R4");
/// var status = r4?.BindingOf("Observation.status");   // http://hl7.org/fhir/ValueSet/observation-status, required
/// var date = r4?.SearchParameterOf("Observation", "date");
/// </code>
/// </example>
public static class FhirMetadata
{
    private static readonly Lazy<FhirReleaseMetadata> s_stu3 = new(Stu3Metadata.Create);
    private static readonly Lazy<FhirReleaseMetadata> s_r4 = new(R4Metadata.Create);
    private static readonly Lazy<FhirReleaseMetadata> s_r4b = new(R4BMetadata.Create);
    private static readonly Lazy<FhirReleaseMetadata> s_r5 = new(R5Metadata.Create);

    /// <summary>
    /// The releases there is metadata of, by their names in <see cref="ReleaseModels"/>.
    /// </summary>
    public static IReadOnlyList<string> Releases { get; } = ["STU3", "R4", "R4B", "R5"];

    /// <summary>
    /// The metadata of a release, built when it is first asked for.
    /// </summary>
    /// <param name="release">The release: <c>STU3</c>, <c>R4</c>, <c>R4B</c>, <c>R5</c> (any case); none is R4, as in <see cref="ReleaseModels.Normalize"/>.</param>
    /// <returns>The metadata, or <see langword="null"/> for a release without it (R6, a ballot).</returns>
    /// <example>
    /// <code>
    /// if (FhirMetadata.Of(server.FhirRelease?.ToString()) is { } metadata)
    /// {
    ///     var binding = metadata.BindingOf("Patient.gender");
    /// }
    /// </code>
    /// </example>
    public static FhirReleaseMetadata? Of(string? release)
    {
        return ReleaseModels.Normalize(release) switch
        {
            "STU3" => s_stu3.Value,
            "R4" => s_r4.Value,
            "R4B" => s_r4b.Value,
            "R5" => s_r5.Value,
            _ => null
        };
    }
}

/// <summary>
/// The metadata of one FHIR release (<see cref="FhirMetadata"/>).
/// </summary>
/// <example>
/// <code>
/// var metadata = FhirMetadata.Of("R4")!;
/// foreach (var binding in metadata.Bindings.Where(b => b.Strength == BindingStrength.Required))
/// {
///     Console.WriteLine($"{binding.Path} {binding.ValueSetUrl}");
/// }
/// </code>
/// </example>
public sealed class FhirReleaseMetadata
{
    private static readonly string[] s_baseTypes = ["DomainResource", "Resource"];

    private readonly FrozenDictionary<string, ElementBinding> _bindings;
    private readonly FrozenDictionary<string, SearchParameterMetadata> _parameters;
    private readonly FrozenSet<string> _resourceTypes;

    /// <summary>
    /// Creates the metadata of a release (the generated classes do).
    /// </summary>
    /// <param name="release">The name of the release (<c>R4</c>).</param>
    /// <param name="packageName">The core package it was generated from.</param>
    /// <param name="packageVersion">The version of that package.</param>
    /// <param name="resourceTypes">The concrete resource types.</param>
    /// <param name="bindings">The bindings of the elements.</param>
    /// <param name="parameters">The search parameters.</param>
    /// <example>
    /// <code>
    /// var metadata = new FhirReleaseMetadata("R4", "hl7.fhir.r4.core", "4.0.1", ["Patient"],
    ///     [new ElementBinding("Patient.gender", "http://hl7.org/fhir/ValueSet/administrative-gender", "4.0.1", BindingStrength.Required)],
    ///     []);
    /// </code>
    /// </example>
    public FhirReleaseMetadata(string release, string packageName, string packageVersion, IReadOnlyList<string> resourceTypes, IReadOnlyList<ElementBinding> bindings,
                               IReadOnlyList<SearchParameterMetadata> parameters)
    {
        ArgumentNullException.ThrowIfNull(resourceTypes);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(parameters);
        Release = release;
        PackageName = packageName;
        PackageVersion = packageVersion;
        ResourceTypes = resourceTypes;
        Bindings = bindings;
        Parameters = parameters;
        _resourceTypes = resourceTypes.ToFrozenSet(StringComparer.Ordinal);
        _bindings = bindings.ToFrozenDictionary(b => b.Path, StringComparer.Ordinal);
        _parameters = parameters.ToFrozenDictionary(p => p.ResourceType + "." + p.Code, StringComparer.Ordinal);
    }

    /// <summary>The name of the release (<c>R4</c>).</summary>
    public string Release { get; }

    /// <summary>The core package the metadata was generated from (<c>hl7.fhir.r4.core</c>).</summary>
    public string PackageName { get; }

    /// <summary>The version of that package (<c>4.0.1</c>).</summary>
    public string PackageVersion { get; }

    /// <summary>The concrete resource types of the release (no abstract one, no datatype), ordered: the types a search form is made for.</summary>
    public IReadOnlyList<string> ResourceTypes { get; }

    /// <summary>The bindings of the elements of the resources and the datatypes of the release, ordered by path.</summary>
    public IReadOnlyList<ElementBinding> Bindings { get; }

    /// <summary>The search parameters, one per resource type of their base, ordered by type and code.</summary>
    public IReadOnlyList<SearchParameterMetadata> Parameters { get; }

    /// <summary>
    /// Whether a name is a concrete resource type of the release.
    /// </summary>
    /// <param name="resourceType">The name (<c>Patient</c>); the case matters.</param>
    /// <returns><see langword="true"/> for a resource type; <see langword="false"/> for a datatype (<c>HumanName</c>), an abstract type or an unknown name.</returns>
    /// <example>
    /// <code>
    /// var ok = FhirMetadata.Of("R4")!.IsResourceType("Observation");   // true
    /// </code>
    /// </example>
    public bool IsResourceType(string resourceType)
    {
        return resourceType is not null && _resourceTypes.Contains(resourceType);
    }

    /// <summary>
    /// The search parameters of a resource type: its own, then those of all resources (<c>Resource</c>) and, unless it is <c>Bundle</c>,
    /// <c>Binary</c> or <c>Parameters</c>, of all domain resources (<c>DomainResource</c>), a code once.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <returns>The parameters, its own first; empty for a type the release does not have.</returns>
    /// <example>
    /// <code>
    /// var codes = FhirMetadata.Of("R4")!.ParametersOf("Patient").Select(p => p.Code);   // birthdate, family... then _id, _lastUpdated...
    /// </code>
    /// </example>
    public IReadOnlyList<SearchParameterMetadata> ParametersOf(string resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        if (!IsResourceType(resourceType))
        {
            return [];
        }

        var own = Parameters.Where(p => p.ResourceType == resourceType).ToList();
        var codes = own.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var inherited = Parameters.Where(p => (p.ResourceType == "Resource" || (p.ResourceType == "DomainResource" && resourceType is not ("Bundle" or "Binary" or "Parameters")))
                                              && codes.Add(p.Code));
        return [.. own, .. inherited];
    }

    /// <summary>
    /// The binding of an element by its path in its resource or datatype.
    /// </summary>
    /// <param name="path">The path (<c>Observation.status</c>, <c>Address.use</c>); a choice is named as in the definition (<c>Observation.value[x]</c>).</param>
    /// <returns>The binding, or <see langword="null"/> for an element that has none.</returns>
    /// <example>
    /// <code>
    /// var gender = FhirMetadata.Of("R4")!.BindingOf("Patient.gender")!.ValueSetUrl;   // http://hl7.org/fhir/ValueSet/administrative-gender
    /// </code>
    /// </example>
    public ElementBinding? BindingOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _bindings.GetValueOrDefault(path);
    }

    /// <summary>
    /// A search parameter of a resource type; one of all the resources (<c>_id</c>, <c>_lastUpdated</c>) is found too.
    /// </summary>
    /// <param name="resourceType">The resource type (<c>Observation</c>).</param>
    /// <param name="code">The code of the parameter (<c>date</c>).</param>
    /// <returns>The parameter, or <see langword="null"/> when the release has none of that code for the type.</returns>
    /// <example>
    /// <code>
    /// var date = FhirMetadata.Of("R4")!.SearchParameterOf("Observation", "date");
    /// var comparators = date?.Comparators;   // eq, ne, gt, lt, ge, le, sa, eb, ap
    /// </code>
    /// </example>
    public SearchParameterMetadata? SearchParameterOf(string resourceType, string code)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        ArgumentNullException.ThrowIfNull(code);
        if (_parameters.GetValueOrDefault(resourceType + "." + code) is { } own)
        {
            return own;
        }

        foreach (var type in s_baseTypes)
        {
            if (_parameters.GetValueOrDefault(type + "." + code) is { } common)
            {
                return common;
            }
        }

        return null;
    }
}

/// <summary>
/// The value set an element is bound to.
/// </summary>
/// <param name="Path">The path of the element (<c>Observation.status</c>).</param>
/// <param name="ValueSetUrl">The canonical URL of the value set, without its version.</param>
/// <param name="ValueSetVersion">The version the definition names (<c>4.0.1</c>), or <see langword="null"/>.</param>
/// <param name="Strength">How strongly the element is bound.</param>
/// <example>
/// <code>
/// var binding = new ElementBinding("Patient.gender", "http://hl7.org/fhir/ValueSet/administrative-gender", "4.0.1", BindingStrength.Required);
/// </code>
/// </example>
public sealed record ElementBinding(string Path, string ValueSetUrl, string? ValueSetVersion, BindingStrength Strength);

/// <summary>
/// The strength of a binding, as FHIR defines it.
/// </summary>
public enum BindingStrength
{
    /// <summary>Only codes of the value set.</summary>
    Required,

    /// <summary>A code of the value set when one fits.</summary>
    Extensible,

    /// <summary>The value set is recommended.</summary>
    Preferred,

    /// <summary>The value set is an example.</summary>
    Example
}

/// <summary>
/// A search parameter of a resource type of its base, with what a search form needs of it.
/// </summary>
/// <param name="ResourceType">A resource type of its base (<c>Resource</c> and <c>DomainResource</c> for those of all resources).</param>
/// <param name="Code">The code (<c>date</c>).</param>
/// <param name="Type">The type of the parameter.</param>
/// <param name="Url">The canonical URL of its definition.</param>
/// <param name="Expression">Its FHIRPath expression, for all its base types.</param>
/// <param name="Bases">All the types of its base.</param>
/// <param name="Hint">What it searches, for this type: the line of this type of a shared description, else the first sentence; plain text.</param>
/// <param name="Path">The one plain path of the expression for this type (<c>Patient.gender</c>), or <see langword="null"/>.</param>
/// <param name="ElementTypes">The types of the element at <paramref name="Path"/> (<c>boolean</c>, <c>CodeableConcept</c>), empty without a path.</param>
/// <param name="Comparators">The comparators it allows (<c>eq</c>, <c>gt</c>...), empty when the definition names none.</param>
/// <param name="Modifiers">The modifiers it allows (<c>missing</c>, <c>exact</c>...), empty when the definition names none.</param>
/// <param name="MultipleOr">Whether several values are allowed (any of them); <see langword="null"/> when the definition does not say.</param>
/// <param name="MultipleAnd">Whether the parameter may repeat (all of them); <see langword="null"/> when the definition does not say.</param>
/// <param name="Chains">The parameters of the target it may be chained with.</param>
/// <param name="Targets">The resource types a reference parameter refers to.</param>
/// <example>
/// <code>
/// var subject = FhirMetadata.Of("R4")!.SearchParameterOf("Observation", "subject")!;
/// var targets = subject.Targets;   // Group, Device, Patient, Location
/// </code>
/// </example>
public sealed record SearchParameterMetadata(string ResourceType, string Code, SearchParameterType Type, string? Url, string? Expression, IReadOnlyList<string> Bases,
                                             string? Hint, string? Path, IReadOnlyList<string> ElementTypes, IReadOnlyList<string> Comparators,
                                             IReadOnlyList<string> Modifiers, bool? MultipleOr, bool? MultipleAnd, IReadOnlyList<string> Chains,
                                             IReadOnlyList<string> Targets);
