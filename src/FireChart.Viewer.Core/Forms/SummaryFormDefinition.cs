using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The structure of a summary (of a patient, of an encounter: <see cref="Context"/>): which sections it has and what each one asks the FHIR server for.
/// </summary>
/// <remarks>
/// A summary is the component <c>{Type}.summary</c> of the library; each of its <c>Feed</c> blocks is read into a <see cref="SummaryFormSection"/>,
/// which the Viewer loads a page at a time. The searches are ordinary FHIR searches, so a server without special profiles still fills the sections.
/// </remarks>
/// <example>
/// <code>
/// var form = new SummaryFormDefinition { Code = "Patient", Context = "Patient", ShowAge = true };
/// var vitals = new SummaryFormSection { Code = "vitals" };
/// vitals.Title.SetDefault("Vital signs");
/// vitals.Searches.Add(new SummaryFormSearch { ResourceType = "Observation", Query = "category=vital-signs", Sort = "-date" });
/// form.Sections.Add(vitals);
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SummaryFormDefinition
{
    /// <summary>
    /// The version of the format of the form this code writes: 2 (2026-11-10) gave a section its <see cref="SummaryFormSection.Component"/>,
    /// <see cref="SummaryFormSection.Options"/> and <see cref="SummaryFormSection.HideIfEmpty"/>; a form of version 1 reads as one whose
    /// sections are all <see cref="SummaryComponents.Tiles"/>. 3 (2026-11-11) gave the form its <see cref="Context"/> and a search its
    /// <see cref="SummaryFormSearch.ContextParam"/> (<c>patientParam</c> before, still read).
    /// </summary>
    public const int CurrentFormatVersion = 3;

    /// <summary>
    /// The version of the format of the form; <see cref="CurrentFormatVersion"/> for a new form.
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// The code of the form: the name of its file, the type of the record it grows from (<c>Patient</c>, the same as <see cref="Context"/>).
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
    /// The type of the record the summary grows from (<see cref="SummaryContexts"/>): <c>Patient</c> (the default), <c>Encounter</c>; the name of the
    /// file of the form. Its searches refer to that record (<see cref="SummaryFormSearch.ContextParam"/>), the page shows its banner and the banner of
    /// its patient above the sections.
    /// </summary>
    public string Context { get; set; } = SummaryContexts.Patient;

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
    public List<SummaryFormSection> Sections { get; } = [];
}

/// <summary>
/// A section of the patient summary: a block of records from one or several searches, newest first.
/// </summary>
/// <example>
/// <code>
/// var problems = new SummaryFormSection { Code = "problems", HideIfUnsupported = true };
/// problems.Searches.Add(new SummaryFormSearch { ResourceType = "Condition", Query = "clinical-status=active", Sort = "-recorded-date" });
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SummaryFormSection
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
    /// Whether the section is dropped, header and all, when its searches found no record; leave it shown where "nothing" is itself clinical information
    /// (allergies).
    /// </summary>
    public bool HideIfEmpty { get; set; }

    /// <summary>
    /// Where a record of the section opens (<see cref="RecordOpening"/>): <c>sheet</c> over the summary or its own <c>page</c>; <see langword="null"/>:
    /// a sheet, the page for a patient and an encounter.
    /// </summary>
    public string? OpenIn { get; set; }

    /// <summary>
    /// The component that shows the records of the section (<see cref="SummaryComponents"/>): <see cref="SummaryComponents.Tiles"/> by default.
    /// </summary>
    public string Component { get; set; } = SummaryComponents.Tiles;

    /// <summary>
    /// The options of the component, in the shape the component defines (<see cref="TilesOptions"/> for the tiles); <see langword="null"/> for its defaults.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "The JSON of the component, replaced whole, not a collection of the model")]
    public JsonObject? Options { get; set; }

    /// <summary>
    /// How the values of the records of the section are shown (the value of an observation and of its components); <see langword="null"/> shows them as
    /// the records have them.
    /// </summary>
    public ValueFormat? Format { get; set; }

    /// <summary>
    /// The searches the block is made from; their records are shown together, newest first.
    /// </summary>
    public List<SummaryFormSearch> Searches { get; } = [];
}

/// <summary>
/// One search of a section of the patient summary.
/// </summary>
/// <example>
/// <code>
/// var search = new SummaryFormSearch { ResourceType = "Observation", Query = "category=laboratory", Sort = "-date", PageSize = 20 };
/// search.SortLabel.SetDefault("date observed");
/// </code>
/// </example>
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SummaryFormSearch
{
    /// <summary>
    /// The resource type searched (<c>Observation</c>).
    /// </summary>
    [JsonRequired]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The search parameter that refers to the record of the summary: <c>patient</c>, <c>subject</c>, <c>beneficiary</c>, <c>encounter</c>...;
    /// <see langword="null"/> for the one of the context (<see cref="SummaryContexts.DefaultParam"/>).
    /// </summary>
    public string? ContextParam { get; set; }

    /// <summary>
    /// The name of <see cref="ContextParam"/> in a form of version 2 (<c>patientParam</c>): read, never written.
    /// </summary>
    [JsonPropertyName("patientParam")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public string? PatientParam
    {
        get => null;
        set => ContextParam ??= value;
    }

    /// <summary>
    /// The parameter the search refers to the record with: <see cref="ContextParam"/>, else the one of the context.
    /// </summary>
    /// <param name="context">The context of the form (<see cref="SummaryFormDefinition.Context"/>).</param>
    /// <returns>The name of the parameter.</returns>
    /// <example>
    /// <code>
    /// var query = $"{search.ParamOf(form.Context)}={id}";
    /// </code>
    /// </example>
    public string ParamOf(string context)
    {
        return string.IsNullOrWhiteSpace(ContextParam) ? SummaryContexts.DefaultParam(context) : ContextParam.Trim();
    }

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
