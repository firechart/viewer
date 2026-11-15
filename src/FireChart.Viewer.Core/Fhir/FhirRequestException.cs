using System.Net;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// An error status of a FHIR server, with what the server said about it: the text of its <c>OperationOutcome</c>
/// (<see cref="FhirServerHttp.OutcomeText"/>). It is an <see cref="HttpRequestException"/>, so the code that looks at the status code keeps working.
/// </summary>
/// <example>
/// <code>
/// try
/// {
///     await reader.SearchAsync(server, "Observation", FhirSearchState.Parse("patient=p1&amp;_sort=-date"));
/// }
/// catch (FhirRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
/// {
///     Console.WriteLine(ex.Outcome);   // Unknown search-param `date` in sort clause.
/// }
/// </code>
/// </example>
public sealed class FhirRequestException : HttpRequestException
{
    /// <summary>
    /// Makes the error of a status.
    /// </summary>
    /// <param name="message">The message: the status and the address.</param>
    /// <param name="outcome">The text of the <c>OperationOutcome</c> of the answer, or <see langword="null"/> when the answer had none.</param>
    /// <param name="statusCode">The status of the answer.</param>
    /// <example>
    /// <code>
    /// throw new FhirRequestException("400 Bad Request: https://fhir.example.org/Observation", "Unknown search-param `date` in sort clause.", HttpStatusCode.BadRequest);
    /// </code>
    /// </example>
    public FhirRequestException(string message, string? outcome, HttpStatusCode statusCode)
        : base(message, null, statusCode)
    {
        Outcome = outcome;
    }

    /// <summary>
    /// What the server said about the error (the diagnostics of the first error of its <c>OperationOutcome</c>, else its details), or
    /// <see langword="null"/>.
    /// </summary>
    public string? Outcome { get; }
}
