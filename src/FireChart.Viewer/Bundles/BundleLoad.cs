namespace FireChart.Viewer.Bundles;

/// <summary>
/// How a Bundle of <see cref="FhirBundleVisualizer"/> came out: the number of its resources and of the references between them, or why it is not shown.
/// </summary>
/// <param name="Resources">The entries of the Bundle that hold a resource; 0 when it is not shown.</param>
/// <param name="References">The references drawn (between the resources, and to those the Bundle lacks); 0 when it is not shown.</param>
/// <param name="Error">
/// <see langword="null"/> when the Bundle is drawn; else <see cref="InvalidBundle"/> (the text is not a FHIR Bundle), <see cref="Unavailable"/> (its
/// address could not be read) or <see cref="Cancelled"/> (the user stopped its loading).
/// </param>
/// <param name="Message">The text the component shows for the error, in the language of the page; <see langword="null"/> without an error.</param>
/// <remarks>
/// <see cref="FhirBundleVisualizer.OnLoad"/> raises it once for every Bundle the component is given, after the drawing (or the error) is in place;
/// a Bundle replaced before it was read raises nothing.
/// </remarks>
/// <example>
/// <code>
/// &lt;FhirBundleVisualizer Bundle=@_text OnLoad=@(load =&gt; _status = load.Error ?? $"{load.Resources} resources")/&gt;
/// </code>
/// </example>
public sealed record BundleLoad(int Resources, int References, string? Error = null, string? Message = null)
{
    /// <summary>The code of a text that is not a FHIR Bundle (not JSON or XML of FHIR, or another resource).</summary>
    public const string InvalidBundle = "invalid_bundle";

    /// <summary>The code of an address that could not be read (the server refused it or did not answer, or it is not FHIR).</summary>
    public const string Unavailable = "unavailable";

    /// <summary>The code of a loading the user cancelled in its dialog.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Whether the Bundle is drawn.</summary>
    /// <value><see langword="true"/> without an <see cref="Error"/>.</value>
    public bool Succeeded => Error is null;
}
