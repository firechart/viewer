using System.Text.Json;
using FireChart.Viewer.Core.Fhir;
using Flox.Core;

namespace FireChart.Viewer.Pages;

/// <summary>What went wrong with a search, for the error tile of its block.</summary>
public enum SearchFailureKind
{
    /// <summary>The server refused the search: a client error (<c>4xx</c>), usually a parameter or an order it does not know.</summary>
    Refused,

    /// <summary>The server failed to run the search: a server error (<c>5xx</c>).</summary>
    ServerError,

    /// <summary>The server did not answer within its time (<see cref="IViewerServer.TimeoutSeconds"/>).</summary>
    Timeout,

    /// <summary>No answer at all: the server is down, or it does not allow requests from a browser (CORS).</summary>
    Unreachable,

    /// <summary>The answer could not be read (not JSON, not a <c>Bundle</c>).</summary>
    Unreadable,
}

/// <summary>A search that failed: what happened, the status of the answer and what the server said.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Status">The status of the answer (<c>400</c>); <see langword="null"/> when there was none.</param>
/// <param name="Detail">What the server said (its <c>OperationOutcome</c>), or what was wrong with the answer; <see langword="null"/> when nothing.</param>
/// <param name="TimeoutSeconds">The time the server had, for <see cref="SearchFailureKind.Timeout"/>.</param>
/// <example>
/// <code>
/// var failure = SearchFailure.Of(exception, server);
/// var line = $"{failure.Status} {failure.Detail}";   // 400 Unknown search-param `date` in sort clause.
/// </code>
/// </example>
public sealed record SearchFailure(SearchFailureKind Kind, int? Status, string? Detail, int TimeoutSeconds = 0)
{
    /// <summary>What an exception of a search means for its block.</summary>
    /// <param name="exception">The exception of the search.</param>
    /// <param name="server">The server that was asked (its time, for a timeout).</param>
    /// <returns>The failure.</returns>
    /// <example>
    /// <code>
    /// catch (Exception ex) when (ex is not OperationCanceledException)
    /// {
    ///     search.Failure = SearchFailure.Of(ex, server);
    /// }
    /// </code>
    /// </example>
    public static SearchFailure Of(Exception exception, IViewerServer server)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(server);
        return exception switch
        {
            TimeoutException => new SearchFailure(SearchFailureKind.Timeout, null, null, (int)FhirServerHttp.TimeoutOf(server).TotalSeconds),
            HttpRequestException { StatusCode: { } status } http => new SearchFailure((int)status >= 500 ? SearchFailureKind.ServerError : SearchFailureKind.Refused,
                (int)status, (http as FhirRequestException)?.Outcome),
            HttpRequestException => new SearchFailure(SearchFailureKind.Unreachable, null, null),
            JsonException or FloxException => new SearchFailure(SearchFailureKind.Unreadable, null, exception.Message),
            _ => new SearchFailure(SearchFailureKind.Unreadable, null, null),
        };
    }
}
