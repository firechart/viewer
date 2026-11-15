using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core.Search;

/// <summary>
/// The codes of a value set as a FHIR server expands it (<c>ValueSet/$expand</c>), for the choices of a field of a search form.
/// </summary>
/// <param name="Options">The codes, in the order of the server, without the abstract entries (the headings of a hierarchy).</param>
/// <param name="Complete">
/// <see langword="true"/> when these are all the codes of the value set: the list may be filtered on the device. <see langword="false"/> when the server
/// cut the expansion (its <c>total</c> is larger, or it gave as many as were asked and no <c>total</c>): a search asks the server again with a filter.
/// </param>
/// <example>
/// <code>
/// var expansion = ValueSetExpansion.Parse(json, limit: 500);
/// var shown = expansion.Complete ? expansion.Matching("fin") : expansion.Options;
/// </code>
/// </example>
public sealed record ValueSetExpansion(IReadOnlyList<SearchOption> Options, bool Complete)
{
    /// <summary>
    /// Reads the expansion of a <c>ValueSet</c> resource (any FHIR release: <c>expansion.contains</c>, nested ones included).
    /// </summary>
    /// <param name="json">The JSON of the <c>ValueSet</c> the server answered with.</param>
    /// <param name="limit">The most codes taken; an expansion with more is not complete.</param>
    /// <returns>The codes and whether they are all of them.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    /// <exception cref="FormatException">The JSON is not a <c>ValueSet</c> with an expansion.</exception>
    /// <example>
    /// <code>
    /// var expansion = ValueSetExpansion.Parse("""{"resourceType":"ValueSet","expansion":{"contains":[{"system":"s","code":"a","display":"A"}]}}""", 500);
    /// // expansion.Options[0].Key == "s|a", expansion.Complete == true
    /// </code>
    /// </example>
    public static ValueSetExpansion Parse(string json, int limit)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "resourceType") != "ValueSet" || !root.TryGetProperty("expansion", out var expansion)
            || expansion.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The answer is not a ValueSet with an expansion.");
        }

        var options = new List<SearchOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cut = false;
        Collect(expansion);
        var total = expansion.TryGetProperty("total", out var t) && t.TryGetInt32(out var number) ? number : (int?)null;
        var complete = !cut && (total is { } all ? all <= options.Count : options.Count < limit);
        return new ValueSetExpansion(options, complete);

        void Collect(JsonElement node)
        {
            if (!node.TryGetProperty("contains", out var contains) || contains.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var entry in contains.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
            {
                var abstractEntry = entry.TryGetProperty("abstract", out var a) && a.ValueKind == JsonValueKind.True;
                if (!abstractEntry && Text(entry, "code") is { Length: > 0 } code)
                {
                    if (options.Count >= limit)
                    {
                        cut = true;
                        return;
                    }

                    var option = new SearchOption(code, Text(entry, "display") ?? code, Text(entry, "system"));
                    if (seen.Add(option.Key))
                    {
                        options.Add(option);
                    }
                }

                Collect(entry);
            }
        }
    }

    /// <summary>
    /// The codes whose display or code contains the text (ignoring the case); all of them for an empty text. A server may ignore the filter it was asked
    /// for, so the answer is filtered here too.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <returns>The matching codes in their order.</returns>
    /// <example>
    /// <code>
    /// var finals = expansion.Matching("fin");   // "Final (final)"
    /// </code>
    /// </example>
    public IReadOnlyList<SearchOption> Matching(string? text)
    {
        var filter = text?.Trim();
        return string.IsNullOrEmpty(filter)
            ? Options
            : [.. Options.Where(o => o.Display.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || o.Code.Contains(filter, StringComparison.OrdinalIgnoreCase))];
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}

/// <summary>
/// Expands value sets on the FHIR server of the Viewer (<c>ValueSet/$expand</c>), with the credentials of that server; no terminology server is asked.
/// </summary>
/// <remarks>
/// A server that does not expand a value set (it does not support the operation, does not know the value set or its code system, or the token may not
/// read value sets) answers with an error: the field then stays a text box. Such a refusal is remembered for the server and the value set, so it is not
/// asked again; an expansion without a filter is kept too. Only the URL of the value set and the text typed leave the device, no patient data.
/// </remarks>
/// <example>
/// <code>
/// var expansion = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-status", null, null);
/// if (expansion is not null)
/// {
///     options = expansion.Options;
/// }
/// </code>
/// </example>
public interface IValueSetExpander
{
    /// <summary>
    /// The codes of a value set as the server expands it, in the language of the current UI culture where the server can.
    /// </summary>
    /// <param name="server">The FHIR server.</param>
    /// <param name="valueSetUrl">The canonical URL of the value set.</param>
    /// <param name="version">The version of the value set, or <see langword="null"/> for the server's.</param>
    /// <param name="filter">The text typed (the parameter <c>filter</c>), or <see langword="null"/> for the whole value set.</param>
    /// <param name="cancellationToken">Cancels the request (a newer text was typed).</param>
    /// <returns>The expansion, or <see langword="null"/> when the server does not expand the value set; never throws for a server that fails.</returns>
    Task<ValueSetExpansion?> ExpandAsync(IViewerServer server, string valueSetUrl, string? version, string? filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// The display of a code the server gave in an expansion of the value set, in the current UI culture; nothing is asked.
    /// </summary>
    /// <param name="server">The FHIR server.</param>
    /// <param name="valueSetUrl">The canonical URL of the value set.</param>
    /// <param name="version">The version of the value set, or <see langword="null"/>.</param>
    /// <param name="token">The value of the search as the query has it (<see cref="SearchOption.Key"/>: <c>system|code</c> or a code).</param>
    /// <returns>The display, or <see langword="null"/> when no expansion of this server had the code.</returns>
    /// <example>
    /// <code>
    /// var text = expander.DisplayOf(server, "http://hl7.org/fhir/ValueSet/observation-status", null, "http://hl7.org/fhir/observation-status|final") ?? token;
    /// </code>
    /// </example>
    string? DisplayOf(IViewerServer server, string valueSetUrl, string? version, string token);
}

/// <summary>
/// The <see cref="IValueSetExpander"/> over HTTP; a singleton, its memory is per server, value set and language.
/// </summary>
/// <param name="httpClientFactory">Makes the client <see cref="FhirServerHttp.HttpClientName"/>.</param>
/// <param name="credentials">The <c>Authorization</c> of the servers.</param>
/// <param name="logger">Logs a refusal (the server and the value set, no patient data).</param>
/// <example>
/// <code>
/// var expander = new ValueSetExpander(httpClientFactory, credentials, logger);
/// var expansion = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);
/// </code>
/// </example>
[FloxService(typeof(IValueSetExpander), ServiceLifetime.Singleton)]
public sealed class ValueSetExpander(IHttpClientFactory httpClientFactory, IServerCredentialStore credentials, ILogger<ValueSetExpander> logger) : IValueSetExpander
{
    /// <summary>
    /// The most codes asked for and kept: a larger value set is searched on the server as the user types.
    /// </summary>
    public const int Limit = 500;

    private readonly ConcurrentDictionary<string, ValueSetExpansion> _whole = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _refused = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _displays = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string? DisplayOf(IViewerServer server, string valueSetUrl, string? version, string token)
    {
        ArgumentNullException.ThrowIfNull(server);
        return _displays.GetValueOrDefault($"{KeyOf(server, valueSetUrl, version)}|{token}");
    }

    private static string KeyOf(IViewerServer server, string valueSetUrl, string? version)
    {
        return $"{server.BaseUrl}|{valueSetUrl}|{version}|{CultureInfo.CurrentUICulture.Name}";
    }

    // Every code an answer had keeps its display for the chips of the applied search
    private ValueSetExpansion Remember(string key, ValueSetExpansion expansion)
    {
        foreach (var option in expansion.Options)
        {
            _displays[$"{key}|{option.Key}"] = option.Display;
        }

        return expansion;
    }

    /// <inheritdoc/>
    public async Task<ValueSetExpansion?> ExpandAsync(IViewerServer server, string valueSetUrl, string? version, string? filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueSetUrl);
        var language = CultureInfo.CurrentUICulture.Name;
        var key = KeyOf(server, valueSetUrl, version);
        var text = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
        if (_refused.ContainsKey(key))
        {
            return null;
        }

        if (_whole.TryGetValue(key, out var known) && (text is null || known.Complete))
        {
            return text is null ? known : known with { Options = known.Matching(text) };
        }

        var url = $"{server.BaseUrl.TrimEnd('/')}/ValueSet/$expand?url={Uri.EscapeDataString(valueSetUrl)}"
                  + (string.IsNullOrWhiteSpace(version) ? string.Empty : $"&valueSetVersion={Uri.EscapeDataString(version)}")
                  + (text is null ? string.Empty : $"&filter={Uri.EscapeDataString(text)}")
                  + $"&count={Limit}"
                  + (language.Length == 0 ? string.Empty : $"&displayLanguage={Uri.EscapeDataString(language)}");
        try
        {
            using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, url, credentials.GetAuthorization(server), FhirServerHttp.TimeoutOf(server), cancellationToken);
            var expansion = Remember(key, ValueSetExpansion.Parse(document.RootElement.GetRawText(), Limit));
            if (text is null)
            {
                _whole[key] = expansion;
                return expansion;
            }

            // A server may ignore the filter and answer with the whole value set
            return expansion with { Options = expansion.Matching(text) };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TimeoutException)
        {
            if (ex is not HttpRequestException { StatusCode: null or >= HttpStatusCode.InternalServerError and not HttpStatusCode.NotImplemented } and not TimeoutException)
            {
                _refused[key] = 0;
            }

            logger.LogInformation("{Server} does not expand the value set {ValueSet}: {Error}", server.BaseUrl, valueSetUrl, ex.Message);
            return null;
        }
    }
}
