using System.Text.Json.Serialization;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The search form of one resource type of one FHIR release: the fields a person fills to search a FHIR server, and how they are laid out.
/// </summary>
/// <remarks>
/// <para>
/// The vendor library holds one form per release and resource type as <c>search-forms/{release}/{type}.json</c> (<c>search-forms/R4/Patient.json</c>);
/// a customer may override a form by a layer of their own. A form is self-contained: the options of a coded field are in the form itself
/// (<see cref="SearchFormField.Options"/>), so it needs neither a database of packages nor a terminology server.
/// </para>
/// <para>
/// It is read and written by <see cref="ViewerJson"/>: camelCase names, enumerations as camelCase strings, the keys of the texts as they are.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var form = new SearchFormDefinition { Name = "default", AuthoredForVersion = "4.0.1" };
/// form.Title.SetDefault("Find patients");
/// form.Fields.Add(new SearchFormField { QueryName = "family", ParameterType = SearchParameterType.String, Order = 1 });
/// File.WriteAllText("search-forms/R4/Patient.json", ViewerJson.Write(form));
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SearchFormDefinition
{
    /// <summary>
    /// The version of the format of the form this code writes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// The version of the format of the form; <see cref="CurrentFormatVersion"/> for a new form.
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// The technical name of the form (<c>default</c>).
    /// </summary>
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The version of the package (the FHIR core package of the release) the form was tuned for; informational.
    /// </summary>
    public string? AuthoredForVersion { get; set; }

    /// <summary>
    /// Whether the form was made from the search parameters of the release and not tuned by a person yet.
    /// </summary>
    public bool IsGenerated { get; set; }

    /// <summary>
    /// The title of the form.
    /// </summary>
    public LocalizedString Title { get; } = [];

    /// <summary>
    /// A description of the form, for the person who fills it.
    /// </summary>
    public LocalizedString Description { get; } = [];

    /// <summary>
    /// The groups of the fields, in any order (<see cref="SearchFormGroup.Order"/> orders them).
    /// </summary>
    public List<SearchFormGroup> Groups { get; } = [];

    /// <summary>
    /// The fields of the form.
    /// </summary>
    public List<SearchFormField> Fields { get; } = [];
}

/// <summary>
/// A group of fields of a search form.
/// </summary>
/// <example>
/// <code>
/// var group = new SearchFormGroup { Code = "dates", Order = 2, CollapsedByDefault = true };
/// group.Caption.SetDefault("Dates");
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SearchFormGroup
{
    /// <summary>
    /// The code of the group, unique within the form; a field names its group by it (<see cref="SearchFormField.GroupCode"/>).
    /// </summary>
    [JsonRequired]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The caption of the group.
    /// </summary>
    public LocalizedString Caption { get; } = [];

    /// <summary>
    /// The position of the group in the form.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether the group is closed when the form opens.
    /// </summary>
    public bool CollapsedByDefault { get; set; }
}

/// <summary>
/// A field of a search form: one search parameter sent to the FHIR server.
/// </summary>
/// <example>
/// <code>
/// var gender = new SearchFormField { QueryName = "gender", ParameterType = SearchParameterType.Token, Control = SearchFieldControl.Select };
/// gender.Label.SetDefault("Gender");
/// gender.Options.Add(new SearchFormOption { Code = "female", System = "http://hl7.org/fhir/administrative-gender" });
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SearchFormField
{
    /// <summary>
    /// The name of the query parameter sent to the server and the key of the field within the form: a parameter code (<c>birthdate</c>), a chain
    /// (<c>patient.name</c>, <c>subject:Patient.name</c>) or anything the designer wrote by hand.
    /// </summary>
    [JsonRequired]
    public string QueryName { get; set; } = string.Empty;

    /// <summary>
    /// Where the query name comes from.
    /// </summary>
    public SearchFieldOrigin Origin { get; set; } = SearchFieldOrigin.Package;

    /// <summary>
    /// The type of the value sent (for a chain: of its last parameter).
    /// </summary>
    public SearchParameterType ParameterType { get; set; } = SearchParameterType.String;

    /// <summary>
    /// Whether the field is shown when the form opens; a field that is not is offered under "more filters".
    /// </summary>
    public bool Included { get; set; } = true;

    /// <summary>
    /// The label of the field.
    /// </summary>
    public LocalizedString Label { get; } = [];

    /// <summary>
    /// A hint under the field.
    /// </summary>
    public LocalizedString Hint { get; } = [];

    /// <summary>
    /// The placeholder of an empty field.
    /// </summary>
    public LocalizedString Placeholder { get; } = [];

    /// <summary>
    /// Whether the form asks for a value (a rule of the form only: FHIR has no required search parameters).
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// The control the field is edited with.
    /// </summary>
    public SearchFieldControl Control { get; set; } = SearchFieldControl.Text;

    /// <summary>
    /// The code of the group of the field, or <see langword="null"/> for the form itself.
    /// </summary>
    public string? GroupCode { get; set; }

    /// <summary>
    /// The position of the field within its group, or within the form when there is no group.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The width of the field in a grid of 12 columns, or <see langword="null"/> for the default.
    /// </summary>
    public int? Span { get; set; }

    /// <summary>
    /// The value the field has when the form opens.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// The canonical URL of the value set the options were taken from; informational once <see cref="Options"/> are filled.
    /// </summary>
    public string? ValueSetUrl { get; set; }

    /// <summary>
    /// The version of <see cref="ValueSetUrl"/>.
    /// </summary>
    public string? ValueSetVersion { get; set; }

    /// <summary>
    /// The options of a coded field (a select, a multi-select, an autocomplete): the expanded value set, so the form needs no terminology server.
    /// </summary>
    public List<SearchFormOption> Options { get; } = [];

    /// <summary>
    /// Whether a code may be typed in when the options do not offer it.
    /// </summary>
    public bool AllowFreeText { get; set; }

    /// <summary>
    /// Whether several values may be given, joined by a comma (OR).
    /// </summary>
    public bool MultipleOr { get; set; }

    /// <summary>
    /// Whether the parameter may be repeated (AND).
    /// </summary>
    public bool MultipleAnd { get; set; }

    /// <summary>
    /// The comparators (<c>ge</c>, <c>le</c>, ...) the person may use.
    /// </summary>
    public List<string> Comparators { get; } = [];

    /// <summary>
    /// The comparator preselected, or <see langword="null"/>.
    /// </summary>
    public string? DefaultComparator { get; set; }

    /// <summary>
    /// The modifiers (<c>:exact</c>, <c>:contains</c>, ...) the person may use.
    /// </summary>
    public List<string> Modifiers { get; } = [];

    /// <summary>
    /// The modifier preselected, or <see langword="null"/>.
    /// </summary>
    public string? DefaultModifier { get; set; }

    /// <summary>
    /// The shortest text accepted, or <see langword="null"/>.
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// The longest text accepted, or <see langword="null"/>.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// The lower bound of a number or a date, in the invariant text form, or <see langword="null"/>.
    /// </summary>
    public string? Min { get; set; }

    /// <summary>
    /// The upper bound of a number or a date, in the invariant text form, or <see langword="null"/>.
    /// </summary>
    public string? Max { get; set; }

    /// <summary>
    /// A regular expression the text must match, or <see langword="null"/>.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// The UCUM units a quantity field offers.
    /// </summary>
    public List<QuantityUnit> Units { get; } = [];

    /// <summary>
    /// The unit preselected, or <see langword="null"/>.
    /// </summary>
    public string? DefaultUnit { get; set; }

    /// <summary>
    /// How a reference field finds the resource it refers to, or <see langword="null"/>.
    /// </summary>
    public ReferenceLookup? Lookup { get; set; }

    /// <summary>
    /// Where the field comes from in the package, set by the designer; informational.
    /// </summary>
    public FieldSource? Source { get; set; }

    /// <summary>
    /// Whether a later version of the package no longer has the parameter of the field.
    /// </summary>
    public bool NotInPackage { get; set; }
}

/// <summary>
/// An option of a coded field: a code of the expanded value set and how it is shown.
/// </summary>
/// <example>
/// <code>
/// var option = new SearchFormOption { Code = "female", System = "http://hl7.org/fhir/administrative-gender" };
/// option.Display.SetDefault("Female");
/// option.Display["ru"] = "Женский";
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SearchFormOption
{
    /// <summary>
    /// The code sent to the server.
    /// </summary>
    [JsonRequired]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The code system of the code, or <see langword="null"/> when the parameter takes the bare code; sent as <c>system|code</c> when set.
    /// </summary>
    public string? System { get; set; }

    /// <summary>
    /// How the code is shown.
    /// </summary>
    public LocalizedString Display { get; } = [];
}

/// <summary>
/// A UCUM unit a quantity field offers.
/// </summary>
/// <example>
/// <code>
/// var unit = new QuantityUnit { Code = "mm[Hg]" };
/// unit.Label.SetDefault("mmHg");
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class QuantityUnit
{
    /// <summary>
    /// The UCUM code (<c>mg</c>, <c>mm[Hg]</c>).
    /// </summary>
    [JsonRequired]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// How the unit is shown.
    /// </summary>
    public LocalizedString Label { get; } = [];
}

/// <summary>
/// How a reference field finds the resource it refers to.
/// </summary>
/// <example>
/// <code>
/// var lookup = new ReferenceLookup { LookupParameter = "name", DisplayTemplate = "{name.family} ({birthDate})" };
/// lookup.TargetTypes.Add("Patient");
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class ReferenceLookup
{
    /// <summary>
    /// The resource types the reference may point to.
    /// </summary>
    public List<string> TargetTypes { get; } = [];

    /// <summary>
    /// How the person picks the resource.
    /// </summary>
    public ReferenceLookupMode Mode { get; set; } = ReferenceLookupMode.Dialog;

    /// <summary>
    /// The search parameter of the target resource the lookup queries by (<c>name</c> for Patient), or <see langword="null"/>.
    /// </summary>
    public string? LookupParameter { get; set; }

    /// <summary>
    /// How the chosen resource is shown, with element paths in braces (<c>{name.family} ({birthDate})</c>), or <see langword="null"/>.
    /// </summary>
    public string? DisplayTemplate { get; set; }

    /// <summary>
    /// How many characters are typed before the lookup asks the server.
    /// </summary>
    public int MinChars { get; set; } = 2;
}

/// <summary>
/// Where a field comes from in the package; set by the designer, informational.
/// </summary>
/// <example>
/// <code>
/// var source = new FieldSource { ParameterUrl = "http://hl7.org/fhir/SearchParameter/individual-birthdate", Expression = "Patient.birthDate" };
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class FieldSource
{
    /// <summary>
    /// The canonical URL of the SearchParameter, or <see langword="null"/>.
    /// </summary>
    public string? ParameterUrl { get; set; }

    /// <summary>
    /// The FHIRPath expression of the parameter, or <see langword="null"/>.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// The resource types the parameter is defined for.
    /// </summary>
    public List<string> Base { get; } = [];
}

/// <summary>
/// The types of FHIR search parameters. <see cref="Composite"/> and <see cref="Special"/> are in the model and not offered by the designer yet.
/// </summary>
public enum SearchParameterType
{
    /// <summary>A string parameter.</summary>
    String,

    /// <summary>A token: a code, an identifier, a boolean.</summary>
    Token,

    /// <summary>A date or a date and time.</summary>
    Date,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A quantity: a number with a unit.</summary>
    Quantity,

    /// <summary>A reference to another resource.</summary>
    Reference,

    /// <summary>A URI.</summary>
    Uri,

    /// <summary>A composite of other parameters.</summary>
    Composite,

    /// <summary>A parameter with special rules.</summary>
    Special
}

/// <summary>
/// The control a field is edited with.
/// </summary>
public enum SearchFieldControl
{
    /// <summary>A line of text.</summary>
    Text,

    /// <summary>A number.</summary>
    Number,

    /// <summary>Two numbers: from and to.</summary>
    NumberRange,

    /// <summary>A yes or no.</summary>
    Boolean,

    /// <summary>A date.</summary>
    Date,

    /// <summary>Two dates: from and to.</summary>
    DateRange,

    /// <summary>One of the options.</summary>
    Select,

    /// <summary>Several of the options.</summary>
    MultiSelect,

    /// <summary>The options filtered as the person types.</summary>
    Autocomplete,

    /// <summary>A reference to a resource, found by <see cref="ReferenceLookup"/>.</summary>
    Reference,

    /// <summary>A number with a unit.</summary>
    Quantity,

    /// <summary>A system and a value.</summary>
    Identifier
}

/// <summary>
/// Where the query name of a field comes from.
/// </summary>
public enum SearchFieldOrigin
{
    /// <summary>A search parameter defined in the package (or in the packages it depends on).</summary>
    Package,

    /// <summary>A chain composed in the designer (<c>patient.name</c>): the person sees one plain field.</summary>
    Chained,

    /// <summary>Written by hand, not checked against the package (a parameter a particular server adds).</summary>
    Custom
}

/// <summary>
/// How the person picks the resource of a reference field.
/// </summary>
public enum ReferenceLookupMode
{
    /// <summary>A button next to the field opens a lookup dialog.</summary>
    Dialog,

    /// <summary>A dropdown under the field.</summary>
    Dropdown
}
