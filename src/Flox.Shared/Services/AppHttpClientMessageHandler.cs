using System.Net.Http.Headers;

namespace Flox.Shared.Services;

[FloxService(ServiceLifetime.Transient)]
public sealed class AppHttpClientMessageHandler
(
    IAuthenticationService authenticationService
) : DelegatingHandler
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> s_current = new AsyncLocal<IReadOnlyDictionary<string, string>?>();


    /// <summary>
    /// Sets query parameters for the duration of the returned scope.
    /// Dispose the returned value to clear the parameters.
    /// </summary>
    public static IDisposable Push(IReadOnlyDictionary<string, string> parameters)
    {
        s_current.Value = parameters;
        return new Scope();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await authenticationService.GetAccessTokenAsync();
        var attached = false;
        if (!string.IsNullOrWhiteSpace(accessToken) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            attached = true;
        }

        if (attached && request.Content is not null)
        {
            // A request that may have to go again can be read again
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        var parameters = s_current.Value;
        if (parameters is { Count: > 0 })
        {
            var uri = request.RequestUri!;
            var existing = uri.Query.TrimStart('?');
            var extra = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            var combined = string.IsNullOrEmpty(existing) ? extra : $"{existing}&{extra}";
            request.RequestUri = new UriBuilder(uri)
            {
                Query = combined
            }.Uri;
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && attached)
        {
            // The server refused a token that looked good: one renewal that does not wait for the expiry, and the request goes again with what it
            // gives. Once: a second refusal is the end, not a loop
            var renewed = await authenticationService.RefreshAccessTokenAsync(accessToken!);
            if (!string.IsNullOrWhiteSpace(renewed) && renewed != accessToken)
            {
                response.Dispose();
                using var again = await CloneAsync(request, renewed, cancellationToken);
                response = await base.SendAsync(again, cancellationToken);
            }
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Should be handled by Blazor
            throw new UnauthenticatedException();
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, string accessToken, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers.Where(row => !string.Equals(row.Key, "Authorization", StringComparison.OrdinalIgnoreCase)))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        foreach (var option in (IDictionary<string, object?>)request.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        if (request.Content is not null)
        {
            var content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            s_current.Value = null;
        }
    }
}
