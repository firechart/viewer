namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The records a summary grows from (<see cref="SummaryFormDefinition.Context"/>): any resource type that has a summary, the component <c>{Type}.summary</c>
/// (the summary of a patient, of an encounter). Its searches refer to that record, its banners show it (and its patient).
/// </summary>
/// <example>
/// <code>
/// SummaryContexts.DefaultParam("Encounter"); // "encounter"
/// </code>
/// </example>
public static class SummaryContexts
{
    /// <summary>The summary of a patient: <c>Patient</c>.</summary>
    public const string Patient = "Patient";

    /// <summary>The summary of an encounter, below the banner of its patient: <c>Encounter</c>.</summary>
    public const string Encounter = "Encounter";

    /// <summary>
    /// The search parameter that refers to the record of a context when a search names none: the type in lower case (<c>patient</c>,
    /// <c>encounter</c>).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The name of the parameter.</returns>
    /// <example>
    /// <code>
    /// var parameter = SummaryContexts.DefaultParam(form.Context); // "patient"
    /// </code>
    /// </example>
    public static string DefaultParam(string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ToLowerInvariant();
    }
}
