using System.Text.Json.Serialization;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The structure of the clinical summary of a patient: which sections it has and what each one asks the FHIR server for.
/// </summary>
/// <remarks>
/// The vendor library holds the forms as <c>summary-forms/{code}.json</c>; <see cref="DefaultCode"/> is the one used when nothing else is chosen. A FHIR
/// server refers to a form by its <see cref="Code"/>; a form of a customer with the code of a vendor form overrides it. The searches are
/// ordinary FHIR searches, so a server without special profiles still fills the sections. It is read and written by <see cref="ViewerJson"/>.
/// </remarks>
/// <example>
/// <code>
/// var form = new ClinicalSummaryFormDefinition { Code = "oncology", ShowAge = true };
/// var vitals = new ClinicalSummaryFormSection { Code = "vitals" };
/// vitals.Title.SetDefault("Vital signs");
/// vitals.Searches.Add(new ClinicalSummaryFormSearch { ResourceType = "Observation", Query = "category=vital-signs", Sort = "-date" });
/// form.Sections.Add(vitals);
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class ClinicalSummaryFormDefinition
{
    /// <summary>
    /// The version of the format of the form this code writes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// The code of the form used when a FHIR server names none: <c>default</c>.
    /// </summary>
    public const string DefaultCode = "default";

    /// <summary>
    /// The version of the format of the form; <see cref="CurrentFormatVersion"/> for a new form.
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// The code of the form: the key a FHIR server refers to it by and the name of its file (<c>default</c>). A form of a customer with the code of a
    /// vendor form overrides it.
    /// </summary>
    [JsonRequired]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The title of the form.
    /// </summary>
    public LocalizedString Title { get; } = [];

    /// <summary>
    /// A description of the form, for the person who chooses it.
    /// </summary>
    public LocalizedString Description { get; } = [];

    /// <summary>
    /// Whether the rows of the sections show the age of the patient at the moment of the record.
    /// </summary>
    public bool ShowAge { get; set; }

    /// <summary>
    /// Whether the rows of the sections show the status of the record as a badge.
    /// </summary>
    public bool ShowStatus { get; set; } = true;

    /// <summary>
    /// The sections, in the order of the page.
    /// </summary>
    public List<ClinicalSummaryFormSection> Sections { get; } = [];
}

/// <summary>
/// A section of the clinical summary: a block of records from one or several searches, newest first.
/// </summary>
/// <example>
/// <code>
/// var problems = new ClinicalSummaryFormSection { Code = "problems", HideIfUnsupported = true };
/// problems.Searches.Add(new ClinicalSummaryFormSearch { ResourceType = "Condition", Query = "clinical-status=active", Sort = "-recorded-date" });
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class ClinicalSummaryFormSection
{
    /// <summary>
    /// The code of the section, unique within the form.
    /// </summary>
    [JsonRequired]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The title of the block; without one the block is titled by the resource type of its first search.
    /// </summary>
    public LocalizedString Title { get; } = [];

    /// <summary>
    /// Whether the section is shown.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether the block is closed when the page opens.
    /// </summary>
    public bool CollapsedByDefault { get; set; }

    /// <summary>
    /// Whether the section is dropped, instead of showing an error, when the server does not have the resource type (its <c>/metadata</c>) or refuses
    /// to search it.
    /// </summary>
    public bool HideIfUnsupported { get; set; } = true;

    /// <summary>
    /// The searches the block is made from; their records are shown together, newest first.
    /// </summary>
    public List<ClinicalSummaryFormSearch> Searches { get; } = [];
}

/// <summary>
/// One search of a section of the clinical summary.
/// </summary>
/// <example>
/// <code>
/// var search = new ClinicalSummaryFormSearch { ResourceType = "Observation", Query = "category=laboratory", Sort = "-date", PageSize = 20 };
/// search.SortLabel.SetDefault("date observed");
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class ClinicalSummaryFormSearch
{
    /// <summary>
    /// The resource type searched (<c>Observation</c>).
    /// </summary>
    [JsonRequired]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The search parameter that refers to the patient: <c>patient</c>, <c>subject</c>, <c>beneficiary</c>...
    /// </summary>
    public string PatientParam { get; set; } = "patient";

    /// <summary>
    /// Other parameters as a FHIR query string (<c>category=vital-signs&amp;status=final</c>), without <c>_count</c>, <c>_sort</c> or the parameter of the
    /// patient; or <see langword="null"/>.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// The order the server is asked for (<c>-date</c>), or <see langword="null"/> for none.
    /// </summary>
    public string? Sort { get; set; }

    /// <summary>
    /// What the order is by, for the line under the title (<c>date recorded</c>).
    /// </summary>
    public LocalizedString SortLabel { get; } = [];

    /// <summary>
    /// The records of the first page and of every next one, or <see langword="null"/> for the default of the page.
    /// </summary>
    public int? PageSize { get; set; }
}
