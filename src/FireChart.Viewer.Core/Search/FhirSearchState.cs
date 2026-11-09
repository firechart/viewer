
namespace FireChart.Viewer.Core.Search;

/// <summary>
/// The search of a list as a FHIR query (<c>name=value&amp;name:modifier=a,b&amp;_sort=-date</c>): what the address of a list holds, so a list can be
/// passed on as a link. Immutable.
/// </summary>
/// <remarks>
/// The parameters of the application have the prefix <see cref="ApplicationPrefix"/>: they are dropped when a text is read and never go to the server.
/// </remarks>
/// <example>
/// <code>
/// var search = FhirSearchState.Parse("?family=Smith&amp;_sort=-birthdate");
/// var page = await reader.SearchAsync(server, "Patient", search);
/// </code>
/// </example>
public sealed class FhirSearchState
{
    /// <summary>The prefix of the parameters of the application (<c>x-</c>).</summary>
    public const string ApplicationPrefix = "x-";

    /// <summary>The parameter of the page size (<c>_count</c>).</summary>
    public const string CountParameter = "_count";

    /// <summary>The largest page size a search may ask for.</summary>
    public const int MaxPageSize = 1000;

    /// <summary>The search without parameters.</summary>
    public static readonly FhirSearchState Empty = new([]);

    private FhirSearchState(IReadOnlyList<FhirSearchParameter> parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// The parameters as written in the address: the values are not unescaped (a backslash before a comma or a bar is part of the value).
    /// </summary>
    public IReadOnlyList<FhirSearchParameter> Parameters { get; }

    /// <summary>
    /// The search has no parameters.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            return Parameters.Count == 0;
        }
    }

    /// <summary>
    /// The page size the search asks for (<c>_count</c>); <see langword="null"/> when it does not, or the value is not a number from 1 to
    /// <see cref="MaxPageSize"/>.
    /// </summary>
    public int? PageSize
    {
        get
        {
            var value = Parameters.FirstOrDefault(row => IsNamed(row, CountParameter))?.Values[0];
            return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size) && size is >= 1 and <= MaxPageSize ? size : null;
        }
    }

    /// <summary>
    /// Reads a search.
    /// </summary>
    /// <param name="query">The query of an address, with or without the question mark; empty or <see langword="null"/> is <see cref="Empty"/>.</param>
    /// <returns>The search without the parameters of the application.</returns>
    /// <exception cref="FhirSearchException">The text is not a search.</exception>
    /// <example>
    /// <code>
    /// var search = FhirSearchState.Parse(new Uri(address).Query);
    /// </code>
    /// </example>
    public static FhirSearchState Parse(string? query)
    {
        var text = query?.TrimStart('?');
        return new FhirSearchState([.. FhirSearchQuery.Parse(text).Where(row => !row.Name.StartsWith(ApplicationPrefix, StringComparison.OrdinalIgnoreCase))]);
    }

    /// <summary>
    /// Makes a search of parameters.
    /// </summary>
    /// <param name="parameters">The parameters; one without values is dropped.</param>
    /// <returns>The search.</returns>
    /// <example>
    /// <code>
    /// var search = FhirSearchState.From([new FhirSearchParameter("gender", null, ["female"])]);
    /// </code>
    /// </example>
    public static FhirSearchState From(IEnumerable<FhirSearchParameter> parameters)
    {
        return new FhirSearchState([.. parameters.Where(row => row.Values.Count > 0)]);
    }

    /// <summary>
    /// The same search without some of its parameters and with new ones: a search form replaces what it read and keeps the rest.
    /// </summary>
    /// <param name="removed">The parameters to take out, one occurrence each (compared by value).</param>
    /// <param name="added">The parameters to add at the end; one without values is dropped.</param>
    /// <returns>A new search.</returns>
    /// <example>
    /// <code>
    /// var next = search.Replace(read.Consumed, read.Parameters);
    /// </code>
    /// </example>
    public FhirSearchState Replace(IEnumerable<FhirSearchParameter> removed, IEnumerable<FhirSearchParameter> added)
    {
        var rest = Parameters.ToList();
        foreach (var parameter in removed)
        {
            rest.Remove(parameter);
        }

        return new FhirSearchState([.. rest, .. added.Where(row => row.Values.Count > 0)]);
    }

    /// <summary>
    /// The search for an address: percent-encoded, with a colon, a comma and a slash left as they are (they read better and mean the same).
    /// </summary>
    /// <returns>The query without the question mark.</returns>
    /// <example>
    /// <code>
    /// navigation.NavigateTo($"resources/Patient?{search.ToQueryString()}");
    /// </code>
    /// </example>
    public string ToQueryString()
    {
        return Format(Parameters).Replace("%3A", ":", StringComparison.Ordinal)
                                 .Replace("%2C", ",", StringComparison.Ordinal)
                                 .Replace("%2F", "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// The query of the first request of a list: with <c>_count</c> of the search, else of the server.
    /// </summary>
    /// <param name="serverPageSize">The page size of the server (<see cref="Fhir.IViewerServer.PageSize"/>).</param>
    /// <returns>The query without the question mark.</returns>
    /// <example>
    /// <code>
    /// var url = $"{server.BaseUrl}/Patient?{search.ToSearchQuery(server.PageSize)}";
    /// </code>
    /// </example>
    public string ToSearchQuery(int serverPageSize)
    {
        var parameters = Parameters.Where(row => !IsNamed(row, CountParameter))
                                   .Append(new FhirSearchParameter(CountParameter, null, [(PageSize ?? serverPageSize).ToString(System.Globalization.CultureInfo.InvariantCulture)]));
        return Format(parameters);
    }

    /// <summary>
    /// The query that counts the results of the search (<c>_summary=count</c>): the same narrowing, no paging and no order.
    /// </summary>
    /// <returns>The query without the question mark.</returns>
    /// <example>
    /// <code>
    /// var url = $"{server.BaseUrl}/Patient?{search.ToCountQuery()}";
    /// </code>
    /// </example>
    public string ToCountQuery()
    {
        var parameters = Parameters.Where(row => !IsNamed(row, CountParameter) && !IsNamed(row, "_sort"))
                                   .Append(new FhirSearchParameter("_summary", null, ["count"]));
        return Format(parameters);
    }

    private static bool IsNamed(FhirSearchParameter parameter, string name)
    {
        return parameter.Modifier is null && string.Equals(parameter.Name, name, StringComparison.Ordinal);
    }

    private static string Format(IEnumerable<FhirSearchParameter> parameters)
    {
        return FhirSearchQuery.Format(parameters.Select(row => (row.Modifier is null ? row.Name : $"{row.Name}:{row.Modifier}", string.Join(',', row.Values))));
    }
}
