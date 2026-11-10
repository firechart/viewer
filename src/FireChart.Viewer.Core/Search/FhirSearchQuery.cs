namespace FireChart.Viewer.Core.Search;

/// <summary>
/// A text that is not a valid search, or a search over the limits of <see cref="FhirSearchQuery"/>.
/// </summary>
/// <remarks>The message says what is wrong in English and may be shown to the user who typed the search.</remarks>
/// <example>
/// <code>
/// try
/// {
///     var state = FhirSearchState.Parse(userText);
/// }
/// catch (FhirSearchException exception)
/// {
///     error = exception.Message;
/// }
/// </code>
/// </example>
public class FhirSearchException : ArgumentException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What is wrong with the search.</param>
    public FhirSearchException(string message) : base(message)
    {
    }
}

/// <summary>
/// One parameter of a search as FHIR writes it: <c>name[:modifier]=value1,value2</c>; the values are alternatives.
/// </summary>
/// <param name="Name">The name of the parameter (<c>birthdate</c>, <c>_sort</c>).</param>
/// <param name="Modifier">The modifier after the colon (<c>exact</c>, <c>missing</c>), or <see langword="null"/>.</param>
/// <param name="Values">The values, not unescaped: a comma, a bar or a dollar with a backslash before it is part of a value
/// (<see cref="FhirSearchQuery.Unescape"/>).</param>
/// <example>
/// <code>
/// var name = new FhirSearchParameter("name", "exact", ["Smith"]);
/// </code>
/// </example>
public sealed record FhirSearchParameter
(
    string Name,
    string? Modifier,
    IReadOnlyList<string> Values
);

/// <summary>
/// The text form of a search (<c>name=value&amp;name:modifier=value</c>) and its limits.
/// </summary>
/// <remarks>
/// Every name and value is percent-encoded inside the text, so a value may hold an ampersand.
/// </remarks>
/// <example>
/// <code>
/// var parameters = FhirSearchQuery.Parse("family=Smith&amp;birthdate=ge1970-01-01");
/// var query = FhirSearchQuery.Format([("family", "Smith"), ("gender", "female")]);
/// </code>
/// </example>
public static class FhirSearchQuery
{
    /// <summary>The most parameters a search may have.</summary>
    public const int MAX_PARAMETERS = 20;

    /// <summary>The most values all the parameters of a search may have together.</summary>
    public const int MAX_VALUES = 100;

    /// <summary>The longest value, in characters.</summary>
    public const int MAX_VALUE_LENGTH = 1000;

    /// <summary>
    /// Reads the parameters of a search.
    /// </summary>
    /// <param name="query">The text without the question mark; empty or <see langword="null"/> is no parameter.</param>
    /// <returns>The parameters in their order, with their values not unescaped.</returns>
    /// <exception cref="FhirSearchException">A part is not <c>name=value</c>, or the search is over the limits.</exception>
    /// <example>
    /// <code>
    /// var parameters = FhirSearchQuery.Parse("code=http://loinc.org|8867-4,http://loinc.org|8310-5");
    /// // one parameter "code" with two values
    /// </code>
    /// </example>
    public static IReadOnlyList<FhirSearchParameter> Parse(string? query)
    {
        var result = new List<FhirSearchParameter>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var values = 0;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                throw new FhirSearchException($"'{Shorten(part)}' is not a search parameter: name=value is expected.");
            }

            var name = Decode(part[..equals]);
            var modifier = (string?)null;
            var colon = name.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                modifier = name[(colon + 1)..];
                name = name[..colon];
            }

            if (name.Length == 0 || modifier is { Length: 0 })
            {
                throw new FhirSearchException($"'{Shorten(part)}' is not a search parameter.");
            }

            var alternatives = SplitUnescaped(Decode(part[(equals + 1)..]), ',');
            values += alternatives.Count;
            if (result.Count >= MAX_PARAMETERS || values > MAX_VALUES)
            {
                throw new FhirSearchException($"A search has at most {MAX_PARAMETERS} parameters and {MAX_VALUES} values.");
            }

            if (alternatives.Any(row => row.Length > MAX_VALUE_LENGTH))
            {
                throw new FhirSearchException($"A value of a search is at most {MAX_VALUE_LENGTH} characters.");
            }

            result.Add(new FhirSearchParameter(name, modifier, alternatives));
        }

        return result;
    }

    /// <summary>
    /// Writes the query of a request.
    /// </summary>
    /// <param name="parameters">The name with its modifier (<c>name:exact</c>) and the value as FHIR writes it (alternatives joined by commas).</param>
    /// <returns>The percent-encoded query, without the question mark.</returns>
    /// <example>
    /// <code>
    /// var query = FhirSearchQuery.Format([("name:exact", "Smith"), ("_count", "20")]);
    /// // name%3Aexact=Smith&amp;_count=20
    /// </code>
    /// </example>
    public static string Format(IEnumerable<(string Parameter, string Value)> parameters)
    {
        return string.Join('&', parameters.Select(row => $"{Uri.EscapeDataString(row.Parameter)}={Uri.EscapeDataString(row.Value)}"));
    }

    /// <summary>
    /// Splits a value at a separator that has no backslash before it; the parts keep their escapes.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="separator">The separator (<c>,</c> between alternatives, <c>|</c> between a system and a code).</param>
    /// <returns>The parts; one part when there is no separator.</returns>
    /// <example>
    /// <code>
    /// var parts = FhirSearchQuery.SplitUnescaped(@"a\,b,c", ','); // ["a\,b", "c"]
    /// </code>
    /// </example>
    public static IReadOnlyList<string> SplitUnescaped(string text, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == separator)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    /// Removes the escapes of a value: <c>\,</c>, <c>\|</c>, <c>\$</c> and <c>\\</c> become the characters themselves.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <returns>The value without escapes.</returns>
    /// <example>
    /// <code>
    /// var value = FhirSearchQuery.Unescape(@"a\,b"); // "a,b"
    /// </code>
    /// </example>
    public static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    // A sequence that is not a percent-encoded character stays as it is
    private static string Decode(string text)
    {
        return Uri.UnescapeDataString(text);
    }

    private static string Shorten(string text)
    {
        return text.Length <= 40 ? text : text[..40] + "...";
    }
}
