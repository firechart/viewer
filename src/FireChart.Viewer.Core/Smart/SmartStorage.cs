using System.Text.Json;

namespace FireChart.Viewer.Core.Smart;

/// <summary>
/// Where the pending authorizations and the sessions with FHIR servers are kept: in a browser the storage of the tab (gone when the tab closes); the keys
/// start with <c>fhir:</c>.
/// </summary>
/// <example>
/// <code>
/// public sealed class MemoryStorage : ISmartStorage
/// {
///     private readonly Dictionary&lt;string, string&gt; _values = [];
///     public Task&lt;string?&gt; GetAsync(string key) =&gt; Task.FromResult(_values.GetValueOrDefault(key));
///     public Task SetAsync(string key, string value) { _values[key] = value; return Task.CompletedTask; }
///     public Task RemoveAsync(string key) { _values.Remove(key); return Task.CompletedTask; }
/// }
/// </code>
/// </example>
public interface ISmartStorage
{
    /// <summary>
    /// Reads a value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Writes a value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes when it is written.</returns>
    Task SetAsync(string key, string value);

    /// <summary>
    /// Removes a value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>A task that completes when it is removed.</returns>
    Task RemoveAsync(string key);
}

/// <summary>
/// A sign-in on its way: written before the redirect to the authorization server, read once on the way back. It holds what the way back needs of the
/// server, so no registry of servers is asked then.
/// </summary>
/// <param name="State">The <c>state</c> of the authorization request.</param>
/// <param name="ServerKey">The <see cref="Fhir.IViewerServer.Key"/> of the server.</param>
/// <param name="CodeVerifier">The secret of PKCE.</param>
/// <param name="RedirectUri">The <c>redirect_uri</c> of the request.</param>
/// <param name="ReturnUrl">Where the user goes after the sign-in.</param>
/// <param name="BaseUrl">The base address of the server that was signed in to.</param>
/// <param name="ClientId">The client id.</param>
/// <param name="Scope">The scope that was asked for.</param>
/// <param name="Authority">The authority of the server, or <see langword="null"/> for the SMART discovery of the server.</param>
/// <param name="Name">The name of the server, for the logs.</param>
/// <param name="Launched">The sign-in came from an EHR launch.</param>
public sealed record SmartPending(string State, string ServerKey, string CodeVerifier, string RedirectUri, string ReturnUrl, string BaseUrl, string ClientId, string Scope,
                                  string? Authority, string Name, bool Launched);

/// <summary>
/// What the token response brought.
/// </summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="ExpiresAt">When it expires.</param>
/// <param name="PatientId">The patient of the launch context, or <see langword="null"/>.</param>
/// <param name="EncounterId">The encounter of the launch context, or <see langword="null"/>.</param>
/// <param name="FhirUser">The <c>fhirUser</c> of the id token, or <see langword="null"/>.</param>
/// <param name="Scope">The scope that was granted.</param>
public sealed record SmartSession(string AccessToken, DateTimeOffset ExpiresAt, string? PatientId, string? EncounterId, string? FhirUser, string Scope);

/// <summary>
/// The records of the sign-ins in an <see cref="ISmartStorage"/>.
/// </summary>
public static class SmartStorageExtensions
{
    private const string SessionPrefix = "fhir:session:";
    private const string PendingPrefix = "fhir:pending:";
    private const string IssPrefix = "fhir:iss:";

    /// <summary>
    /// Keeps a sign-in on its way.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="pending">The sign-in.</param>
    /// <returns>A task that completes when it is kept.</returns>
    public static async Task SavePendingAsync(this ISmartStorage storage, SmartPending pending)
    {
        await storage.SetAsync(PendingPrefix + pending.State, JsonSerializer.Serialize(pending));
    }

    /// <summary>
    /// Takes a sign-in on its way: it is gone after it is read (one use).
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="state">The <c>state</c> of the way back.</param>
    /// <returns>The sign-in, or <see langword="null"/> when there is none for the state.</returns>
    public static async Task<SmartPending?> TakePendingAsync(this ISmartStorage storage, string state)
    {
        var key = PendingPrefix + state;
        var value = await storage.GetAsync(key);
        await storage.RemoveAsync(key);
        return Read<SmartPending>(value);
    }

    /// <summary>
    /// Keeps the session of a server.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="serverKey">The key of the server.</param>
    /// <param name="baseUrl">The address it was signed in to.</param>
    /// <param name="session">The session.</param>
    /// <returns>A task that completes when it is kept.</returns>
    public static async Task SaveSessionAsync(this ISmartStorage storage, string serverKey, string baseUrl, SmartSession session)
    {
        await storage.SetAsync(SessionPrefix + serverKey, JsonSerializer.Serialize(new StoredSession(baseUrl, session)));
    }

    /// <summary>
    /// The session of a server at an address; a value of another address or a damaged one is removed.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="serverKey">The key of the server.</param>
    /// <param name="baseUrl">The address of the server now.</param>
    /// <returns>The session, or <see langword="null"/>.</returns>
    public static async Task<SmartSession?> LoadSessionAsync(this ISmartStorage storage, string serverKey, string baseUrl)
    {
        var key = SessionPrefix + serverKey;
        var stored = Read<StoredSession>(await storage.GetAsync(key));
        if (stored is { Session: { } session } && string.Equals(stored.BaseUrl, baseUrl, StringComparison.Ordinal))
        {
            return session;
        }

        await storage.RemoveAsync(key);
        return null;
    }

    /// <summary>
    /// Keeps the address an EHR launch gave for a server in this tab.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="serverKey">The key of the server.</param>
    /// <param name="baseUrl">The address of the launch.</param>
    /// <returns>A task that completes when it is kept.</returns>
    public static async Task SaveIssAsync(this ISmartStorage storage, string serverKey, string baseUrl)
    {
        await storage.SetAsync(IssPrefix + serverKey, baseUrl);
    }

    /// <summary>
    /// The address an EHR launch gave for a server in this tab.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="serverKey">The key of the server.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    public static async Task<string?> LoadIssAsync(this ISmartStorage storage, string serverKey)
    {
        return await storage.GetAsync(IssPrefix + serverKey);
    }

    /// <summary>
    /// Forgets the session of a server and the address of its launch.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="serverKey">The key of the server.</param>
    /// <returns>A task that completes when they are gone.</returns>
    public static async Task RemoveSessionAsync(this ISmartStorage storage, string serverKey)
    {
        await storage.RemoveAsync(SessionPrefix + serverKey);
        await storage.RemoveAsync(IssPrefix + serverKey);
    }

    private static T? Read<T>(string? value) where T : class
    {
        try
        {
            return string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record StoredSession(string BaseUrl, SmartSession? Session);
}
