using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The secrets of the servers: a user name and a password, a pasted token, the access token of a sign-in.
/// </summary>
/// <remarks>
/// Kept in memory only (a reload or a restart asks again), never in the record of a server. They go only to the origin of the server
/// (<see cref="IFhirServerReader"/>). The key is <see cref="IViewerServer.Key"/> with <see cref="IViewerServer.BaseUrl"/>: a server pointed elsewhere does
/// not keep the secret of the old address.
/// </remarks>
/// <example>
/// <code>
/// credentials.SetBasic(server, "demo", password);
/// var header = credentials.GetAuthorization(server); // "Basic ZGVtbzo..."
/// </code>
/// </example>
public interface IServerCredentialStore
{
    /// <summary>
    /// The server has a secret that is good now.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <returns><see langword="true"/> when <see cref="GetAuthorization"/> has a value.</returns>
    bool Has(IViewerServer server);

    /// <summary>
    /// The value of the <c>Authorization</c> header of the server.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <returns>The value (<c>Bearer ...</c>, <c>Basic ...</c>), or <see langword="null"/> when there is none or the access token has expired.</returns>
    string? GetAuthorization(IViewerServer server);

    /// <summary>
    /// Keeps a user name and a password (<see cref="AuthenticationType.Basic"/>).
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="userName">The user name; not empty.</param>
    /// <param name="password">The password.</param>
    /// <exception cref="ArgumentException"><paramref name="userName"/> is empty.</exception>
    void SetBasic(IViewerServer server, string userName, string password);

    /// <summary>
    /// Keeps a token the user pasted (<see cref="AuthenticationType.Token"/>); it does not expire.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="token">The token, without <c>Bearer</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty.</exception>
    void SetToken(IViewerServer server, string token);

    /// <summary>
    /// Keeps the access token of a sign-in (<see cref="AuthenticationType.OAuth2"/>); it stops being used shortly before it expires.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="token">The access token.</param>
    /// <param name="expiresAt">When it expires.</param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty.</exception>
    void SetAccessToken(IViewerServer server, string token, DateTimeOffset expiresAt);

    /// <summary>
    /// Forgets the secret of the server.
    /// </summary>
    /// <param name="server">The server.</param>
    void Forget(IViewerServer server);
}

/// <summary>
/// The <see cref="IServerCredentialStore"/> in memory; a singleton.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton&lt;IServerCredentialStore, ServerCredentialStore&gt;();
/// </code>
/// </example>
[FloxService(typeof(IServerCredentialStore), ServiceLifetime.Singleton)]
public sealed class ServerCredentialStore : IServerCredentialStore
{
    // A token that expires within this time is as good as expired
    private static readonly TimeSpan s_expiryMargin = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, Entry> _authorizations = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Has(IViewerServer server)
    {
        return GetAuthorization(server) is not null;
    }

    /// <inheritdoc/>
    public string? GetAuthorization(IViewerServer server)
    {
        return _authorizations.TryGetValue(KeyOf(server), out var entry) && (entry.ExpiresAt is null || entry.ExpiresAt - s_expiryMargin > DateTimeOffset.UtcNow)
            ? entry.Header
            : null;
    }

    /// <inheritdoc/>
    public void SetBasic(IViewerServer server, string userName, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(userName);
        _authorizations[KeyOf(server)] = new Entry("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}")), null);
    }

    /// <inheritdoc/>
    public void SetToken(IViewerServer server, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _authorizations[KeyOf(server)] = new Entry("Bearer " + token.Trim(), null);
    }

    /// <inheritdoc/>
    public void SetAccessToken(IViewerServer server, string token, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _authorizations[KeyOf(server)] = new Entry("Bearer " + token.Trim(), expiresAt);
    }

    /// <inheritdoc/>
    public void Forget(IViewerServer server)
    {
        _authorizations.TryRemove(KeyOf(server), out _);
    }

    // The address is part of the key: a server that is pointed elsewhere does not keep the secret of the old address
    private static string KeyOf(IViewerServer server)
    {
        return $"{server.Key}|{server.BaseUrl.TrimEnd('/')}";
    }

    private sealed record Entry(string Header, DateTimeOffset? ExpiresAt);
}

/// <summary>
/// Whether a server can be read now.
/// </summary>
public enum ServerAccess
{
    /// <summary>It can: a public server, or one with a secret that is good now.</summary>
    Ready,

    /// <summary>A user name and a password, or a token, are to be asked for.</summary>
    NeedsCredentials,

    /// <summary>A sign-in is needed (SMART).</summary>
    NeedsAuthorization,

    /// <summary>The authentication of the server is not supported.</summary>
    NotSupported
}

/// <summary>
/// Helpers around the secrets of the servers.
/// </summary>
public static class ServerAccessExtensions
{
    /// <summary>
    /// Whether the server can be read now.
    /// </summary>
    /// <param name="store">The secrets.</param>
    /// <param name="server">The server.</param>
    /// <returns>What is needed first.</returns>
    /// <example>
    /// <code>
    /// if (credentials.AccessOf(server) == ServerAccess.NeedsCredentials)
    /// {
    ///     showSignIn = true;
    /// }
    /// </code>
    /// </example>
    public static ServerAccess AccessOf(this IServerCredentialStore store, IViewerServer server)
    {
        return server.AuthenticationType switch
        {
            AuthenticationType.None => ServerAccess.Ready,
            AuthenticationType.Basic or AuthenticationType.Token => store.Has(server) ? ServerAccess.Ready : ServerAccess.NeedsCredentials,
            AuthenticationType.OAuth2 => store.Has(server) ? ServerAccess.Ready : ServerAccess.NeedsAuthorization,
            var _ => ServerAccess.NotSupported
        };
    }

    /// <summary>
    /// The server said the credentials are not right (<c>401</c>).
    /// </summary>
    /// <param name="exception">The error of a request.</param>
    /// <returns><see langword="true"/> for an <see cref="HttpRequestException"/> with <c>401</c>.</returns>
    /// <example>
    /// <code>
    /// catch (HttpRequestException exception) when (exception.IsRejected())
    /// {
    ///     credentials.Forget(server);
    /// }
    /// </code>
    /// </example>
    public static bool IsRejected(this Exception exception)
    {
        return exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized };
    }

    /// <summary>
    /// The header of a value of <see cref="IServerCredentialStore.GetAuthorization"/>.
    /// </summary>
    /// <param name="authorization">The value (<c>Bearer abc</c>), or <see langword="null"/>.</param>
    /// <returns>The header, or <see langword="null"/> for no value.</returns>
    /// <example>
    /// <code>
    /// request.Headers.Authorization = ServerAccessExtensions.ToHeader(credentials.GetAuthorization(server));
    /// </code>
    /// </example>
    public static AuthenticationHeaderValue? ToHeader(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization))
        {
            return null;
        }

        var space = authorization.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? new AuthenticationHeaderValue(authorization) : new AuthenticationHeaderValue(authorization[..space], authorization[(space + 1)..]);
    }
}
