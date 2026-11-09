using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core;

/// <summary>
/// The displays of codes from a terminology server (<c>CodeSystem/$lookup</c>).
/// </summary>
/// <remarks>
/// The codes of a patient leave the device for that server, so nothing is asked unless a server is named by the host. The answers are kept for the scope
/// (an hour for a display, ten minutes for a code the server does not know); at most six lookups run at a time. The language of the displays is the current
/// UI culture.
/// </remarks>
/// <example>
/// <code>
/// var address = TerminologyResolver.AddressOf(options.TerminologyServer);
/// if (address is not null)
/// {
///     var map = await resolver.ResolveAsync(address, codes, TimeSpan.FromSeconds(1));
/// }
/// </code>
/// </example>
public interface ITerminologyResolver
{
    /// <summary>
    /// Raised when a lookup that was still running when <see cref="ResolveAsync"/> returned has finished with a display: the pages draw their rows again.
    /// A burst of answers raises it once.
    /// </summary>
    event EventHandler? Resolved;

    /// <summary>
    /// The displays of codes: what is known now and what the server answers within the wait; the other lookups go on and raise <see cref="Resolved"/>.
    /// </summary>
    /// <param name="serverAddress">The base address of the terminology server (<see cref="TerminologyResolver.AddressOf"/>).</param>
    /// <param name="codes">The codes.</param>
    /// <param name="wait">How long to wait for the answers.</param>
    /// <param name="cancellationToken">Cancels the wait (not the lookups).</param>
    /// <returns>The displays found; never throws for a server that fails.</returns>
    Task<TerminologyMap> ResolveAsync(string serverAddress, IReadOnlyCollection<CodeKey> codes, TimeSpan wait, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="ITerminologyResolver"/> over HTTP; scoped (its answers live as long as the scope).
/// </summary>
/// <param name="httpClientFactory">Makes the client <see cref="HttpClientName"/>.</param>
/// <param name="logger">Logs a lookup that failed (the system and the code, no patient data).</param>
/// <example>
/// <code>
/// using var resolver = new TerminologyResolver(httpClientFactory, loggerFactory.CreateLogger&lt;TerminologyResolver&gt;());
/// </code>
/// </example>
[FloxService(typeof(ITerminologyResolver), ServiceLifetime.Scoped)]
public sealed class TerminologyResolver(IHttpClientFactory httpClientFactory, ILogger<TerminologyResolver> logger) : ITerminologyResolver, IDisposable
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> of the terminology servers: one of its own, without the token of the host application.
    /// </summary>
    public const string HttpClientName = "terminology-server";

    private const int Parallel = 6;
    private static readonly TimeSpan s_known = TimeSpan.FromHours(1);
    private static readonly TimeSpan s_unknown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan s_request = TimeSpan.FromSeconds(6);

    private readonly ConcurrentDictionary<string, Answer> _answers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(Parallel);
    private int _notifying;

    /// <inheritdoc/>
    public event EventHandler? Resolved;

    /// <summary>
    /// The base address of a terminology server as the resolver wants it.
    /// </summary>
    /// <param name="address">An address the host named (<c>https://tx.fhir.org/r4/</c>).</param>
    /// <returns>The absolute <c>http(s)</c> address without the trailing slash, or <see langword="null"/> for none or another scheme.</returns>
    /// <example>
    /// <code>
    /// var address = TerminologyResolver.AddressOf(server.TerminologyServerUrl ?? settings.TerminologyServerUrl);
    /// </code>
    /// </example>
    public static string? AddressOf(string? address)
    {
        return Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri.TrimEnd('/') : null;
    }

    /// <inheritdoc/>
    public async Task<TerminologyMap> ResolveAsync(string serverAddress, IReadOnlyCollection<CodeKey> codes, TimeSpan wait, CancellationToken cancellationToken = default)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var now = DateTimeOffset.UtcNow;
        var lookups = new List<Task>();
        foreach (var code in codes)
        {
            var key = KeyOf(serverAddress, language, code);
            if (_answers.TryGetValue(key, out var answer) && now - answer.At < (answer.Display is null ? s_unknown : s_known))
            {
                continue;
            }

            lookups.Add(_running.GetOrAdd(key, _ => LookupAsync(key, serverAddress, language, code)));
        }

        if (lookups.Count > 0)
        {
            await Task.WhenAny(Task.WhenAll(lookups), Task.Delay(wait, cancellationToken));
        }

        var map = new TerminologyMap();
        foreach (var code in codes)
        {
            if (_answers.TryGetValue(KeyOf(serverAddress, language, code), out var answer) && answer.Display is { } display)
            {
                map.Add(code, display);
            }
        }

        return map;
    }

    /// <summary>
    /// Releases the slots of the lookups.
    /// </summary>
    public void Dispose()
    {
        _slots.Dispose();
    }

    private async Task LookupAsync(string key, string serverAddress, string language, CodeKey code)
    {
        await Task.Yield();
        string? display = null;
        try
        {
            await _slots.WaitAsync();
            try
            {
                display = await AskAsync(serverAddress, language, code);
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "The terminology server {Server} did not answer for {System} {Code}", serverAddress, code.System, code.Code);
        }
        finally
        {
            _answers[key] = new Answer(display, DateTimeOffset.UtcNow);
            _running.TryRemove(key, out _);
        }

        if (display is not null)
        {
            NotifySoon();
        }
    }

    // A page of sixty codes has sixty answers: the pages draw once for a burst of them
    private void NotifySoon()
    {
        if (Interlocked.Exchange(ref _notifying, 1) == 1)
        {
            return;
        }

        _ = Task.Delay(200).ContinueWith(_ =>
        {
            Interlocked.Exchange(ref _notifying, 0);
            Resolved?.Invoke(this, EventArgs.Empty);
        }, TaskScheduler.Default);
    }

    private async Task<string?> AskAsync(string serverAddress, string language, CodeKey code)
    {
        using var http = httpClientFactory.CreateClient(HttpClientName);
        using var cancel = new CancellationTokenSource(s_request);
        var url = $"{serverAddress}/CodeSystem/$lookup?system={Uri.EscapeDataString(code.System)}&code={Uri.EscapeDataString(code.Code)}&displayLanguage={Uri.EscapeDataString(language)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/fhir+json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancel.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancel.Token);
        return DisplayOf(document.RootElement);
    }

    /// <summary>
    /// The display of an answer of <c>$lookup</c>: <c>Parameters.parameter[name = display].valueString</c>.
    /// </summary>
    /// <param name="parameters">The <c>Parameters</c> resource.</param>
    /// <returns>The display, or <see langword="null"/> when there is none.</returns>
    /// <example>
    /// <code>
    /// using var answer = JsonDocument.Parse(json);
    /// var display = TerminologyResolver.DisplayOf(answer.RootElement);
    /// </code>
    /// </example>
    public static string? DisplayOf(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("parameter", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var parameter in list.EnumerateArray())
        {
            if (parameter.TryGetProperty("name", out var name) && name.GetString() == "display" && parameter.TryGetProperty("valueString", out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() is { Length: > 0 } display ? display : null;
            }
        }

        return null;
    }

    private static string KeyOf(string serverAddress, string language, CodeKey code)
    {
        return $"{serverAddress}|{language}|{code.System}|{code.Code}";
    }

    private sealed record Answer(string? Display, DateTimeOffset At);
}
