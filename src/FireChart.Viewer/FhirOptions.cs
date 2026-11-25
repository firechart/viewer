using System.ComponentModel.DataAnnotations;
using Flox.Core.Settings;
using Hl7.Fhir.Specification;

namespace FireChart.Viewer;

/// <summary>
/// The FHIR settings of the Viewer: the release assumed by default, the terminology server, how a summary loads its records and how many decimal
/// places a value shows (option <c>fhir</c> of the configuration). FireChart keeps them as its settings group FHIR settings (<c>fhir</c>).
/// </summary>
/// <remarks>
/// A summary loads its blocks in parallel and draws each as it comes, so the blocks below move while those above fill. With <see cref="LoadAll"/> the
/// page is drawn hidden behind a panel of progress (what has come, Cancel) and appears whole; Cancel shows what has come, marked incomplete. A larger
/// <see cref="PageSize"/> asks the server fewer times; a page size of the summary form wins over it. A number is shown with the digits of the record
/// up to <see cref="Decimals"/> places; one with more is rounded (<see cref="DecimalText.Format"/>). A <c>Feed</c> of a component may set its own
/// <c>decimals</c>. Other options start a new session of the Viewer.
/// </remarks>
/// <example>
/// <code>
/// &lt;FhirViewer Server=@server Fhir=@(new FhirOptions { PageSize = 100, LoadAll = true, Decimals = 1, TerminologyServerUrl = "https://tx.fhir.org/r4" })/&gt;
/// </code>
/// </example>
[FloxSettings("fhir")]
[Display(Name = nameof(Strings.Caption_FhirSettings), ResourceType = typeof(Strings), Order = 10)]
public sealed record FhirOptions
{
    /// <summary>The smallest page size accepted.</summary>
    public const int MinPageSize = 1;

    /// <summary>The largest page size accepted (servers cap <c>_count</c> on their own anyway).</summary>
    public const int MaxPageSize = 500;

    /// <summary>The default page size of the searches of a summary: 20.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The default number of decimal places of a value: 2.</summary>
    public const int DefaultDecimals = 2;

    /// <summary>The most decimal places a value may be set to show: 10 (<see cref="DecimalText.MaxPlaces"/>).</summary>
    public const int MaxDecimals = DecimalText.MaxPlaces;

    /// <summary>The options when the host gives none.</summary>
    public static readonly FhirOptions Default = new();

    /// <summary>The release of FHIR assumed where none is known (a package or a server whose release is not chosen); R4 by default.</summary>
    [Display(Name = nameof(Strings.Caption_DefaultFhirVersion), ResourceType = typeof(Strings), Order = 1)]
    [Setting(OptionsSource = typeof(FhirReleaseOptions))]
    public FhirRelease DefaultFhirVersion { get; set; } = FhirRelease.R4;

    /// <summary>
    /// The terminology server for the displays of bare codes; none: the codes are shown as they are and nothing of a patient leaves for another
    /// server. A server that names its own terminology server is shown with that one.
    /// </summary>
    [Display(Name = nameof(Strings.Caption_FhirServerTerminologyUrl), ResourceType = typeof(Strings), Order = 2)]
    public string? TerminologyServerUrl { get; set; }

    /// <summary>
    /// The size of a page (<c>_count</c>) of a search of a summary whose form gives none; 20 by default. Out of
    /// <see cref="MinPageSize"/>..<see cref="MaxPageSize"/> it is clamped (<see cref="EffectivePageSize"/>).
    /// </summary>
    [Display(Name = nameof(Strings.Caption_SummaryPageSize), ResourceType = typeof(Strings), Order = 3)]
    [Range(10, 200)]
    [Setting(Control = SettingControl.Slider, Step = 10)]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Every record of every block of a summary is loaded before the page is shown, behind a panel of progress with Cancel.</summary>
    [Display(Name = nameof(Strings.Caption_SummaryLoadAll), ResourceType = typeof(Strings), Order = 4)]
    public bool LoadAll { get; set; }

    /// <summary>
    /// The most decimal places of a value (a quantity of an observation, any decimal); 2 by default. A value with more is rounded and loses the zeros
    /// the rounding left (<c>100.004</c> is <c>100</c>); one with as many or fewer keeps the digits of the record (<c>1.30</c>). Out of
    /// 0..<see cref="MaxDecimals"/> it is clamped.
    /// </summary>
    [Display(Name = nameof(Strings.Caption_ValueDecimals), ResourceType = typeof(Strings), Order = 5)]
    [Range(0, MaxDecimals)]
    [Setting(Control = SettingControl.Number)]
    public int Decimals { get; set; } = DefaultDecimals;

    /// <summary>The page size within the accepted range.</summary>
    /// <returns>The page size to use.</returns>
    /// <example>
    /// <code>
    /// var size = new FhirOptions { PageSize = 5000 }.EffectivePageSize(); // 500
    /// </code>
    /// </example>
    public int EffectivePageSize()
    {
        return Math.Clamp(PageSize, MinPageSize, MaxPageSize);
    }
}

/// <summary>
/// The releases of FHIR a setting may choose (<see cref="FhirOptions.DefaultFhirVersion"/>).
/// </summary>
/// <example>
/// <code>
/// var options = new FhirReleaseOptions().GetOptions(); // STU3, R4, R4B, R5
/// </code>
/// </example>
public sealed class FhirReleaseOptions : ISettingOptionsSource
{
    /// <summary>
    /// The releases, oldest first.
    /// </summary>
    /// <returns>The options.</returns>
    public IReadOnlyList<SettingOption> GetOptions()
    {
        return
        [
            new SettingOption("STU3", FhirRelease.STU3),
            new SettingOption("R4", FhirRelease.R4),
            new SettingOption("R4B", FhirRelease.R4B),
            new SettingOption("R5", FhirRelease.R5)
        ];
    }
}
