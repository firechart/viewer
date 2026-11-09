using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Jwk;
using Duende.IdentityModel.OidcClient;
using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Hl7.Fhir.Specification;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Core.Smart;

/// <summary>
/// The sign-in to a FHIR server: SMART App Launch, public client, authorization code with PKCE (S256).
/// </summary>
/// <remarks>
/// The page leaves for the authorization server by a full-page redirect and comes back to one callback address of the host; the pending authorization
/// waits in the <see cref="ISmartStorage"/> of the tab with what the way back needs of the server. The access token goes to the
/// <see cref="IServerCredentialStore"/>, the session to the storage of the tab, so a reload of the tab keeps it.
/// </remarks>
/// <example>
/// <code>
/// var url = await authorizer.StartAsync(server, "https://app.example.org/smart/callback", "/server/1/resources", launch: null);
/// navigation.NavigateTo(url, forceLoad: true);
/// // on the callback page:
/// var completion = await authorizer.CompleteAsync(navigation.Uri);
/// navigation.NavigateTo(completion.ReturnUrl);
/// </code>
/// </example>
public interface ISmartAuthorizer
{
    /// <summary>
    /// Starts a sign-in.
    /// </summary>
    /// <param name="server">The server, with its client id and scope (<see cref="IViewerServer.OAuth2ClientId"/>, <see cref="IViewerServer.OAuth2Scope"/>).</param>
    /// <param name="redirectUri">The callback address of the host, as registered with the authorization server.</param>
    /// <param name="returnUrl">Where the user goes after the sign-in.</param>
    /// <param name="launch">The <c>launch</c> of an EHR launch, or <see langword="null"/> for a standalone sign-in.</param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>The address of the authorization server to go to.</returns>
    /// <exception cref="SmartException">The server is not configured for SMART, its discovery failed, or it has no PKCE with S256.</exception>
    Task<string> StartAsync(IViewerServer server, string redirectUri, string returnUrl, string? launch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a sign-in on the way back.
    /// </summary>
    /// <param name="callbackUrl">The whole address of the way back, with <c>code</c> and <c>state</c>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server and where to go.</returns>
    /// <exception cref="SmartException">The state is not one of this tab, the authorization was refused, or the code could not be exchanged.</exception>
    Task<SmartCompletion> CompleteAsync(string callbackUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the session of an earlier sign-in of this tab.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the server can be read with it; <see langword="false"/> when there is none or it is over.</returns>
    Task<bool> RestoreAsync(IViewerServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the token and the session of a server.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when they are gone.</returns>
    Task DisconnectAsync(IViewerServer server, CancellationToken cancellationToken = default);
}

/// <summary>
/// A completed sign-in.
/// </summary>
/// <param name="ServerKey">The <see cref="IViewerServer.Key"/> of the server.</param>
/// <param name="ReturnUrl">Where the user was before the sign-in.</param>
/// <param name="PatientId">The patient of the launch context (a patient picked at the sign-in, or the one of an EHR launch), or <see langword="null"/>:
/// a host starts with that patient (a SMART launch decides the start page).</param>
public sealed record SmartCompletion(string ServerKey, string ReturnUrl, string? PatientId = null);

/// <summary>
/// The <see cref="ISmartAuthorizer"/> over <c>Duende.IdentityModel.OidcClient</c>; scoped.
/// </summary>
/// <param name="httpClientFactory">Makes the client of the FHIR servers (<see cref="FhirServerHttp.HttpClientName"/>).</param>
/// <param name="credentials">Where the access token goes.</param>
/// <param name="storage">The storage of the tab.</param>
/// <example>
/// <code>
/// var authorizer = new SmartAuthorizer(httpClientFactory, credentials, storage);
/// </code>
/// </example>
[FloxService(typeof(ISmartAuthorizer), ServiceLifetime.Scoped)]
public sealed class SmartAuthorizer
(
    IHttpClientFactory httpClientFactory,
    IServerCredentialStore credentials,
    ISmartStorage storage
) : ISmartAuthorizer
{
    private const string PatientField = "patient";
    private const string EncounterField = "encounter";
    private const string FhirUserClaim = "fhirUser";

    /// <inheritdoc/>
    public async Task<string> StartAsync(IViewerServer server, string redirectUri, string returnUrl, string? launch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        var scope = ScopeOf(server, launch);
        var client = await CreateClientAsync(server, redirectUri, scope, cancellationToken);
        var parameters = new Parameters { { "aud", server.BaseUrl.TrimEnd('/') } };
        if (!string.IsNullOrEmpty(launch))
        {
            parameters.Add("launch", launch);
        }

        foreach (var pair in ExtraParametersOf(server))
        {
            parameters.Add(pair.Key, pair.Value);
        }

        var state = await client.PrepareLoginAsync(parameters, cancellationToken);
        await storage.SavePendingAsync(new SmartPending(state.State, server.Key, state.CodeVerifier, state.RedirectUri, returnUrl, server.BaseUrl, server.OAuth2ClientId!,
                                                        server.OAuth2Scope!, server.Authority, server.Name, !string.IsNullOrEmpty(launch)));
        return state.StartUrl;
    }

    /// <inheritdoc/>
    public async Task<SmartCompletion> CompleteAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        var response = QueryOf(callbackUrl);
        if (!response.TryGetValue("state", out var stateValue) || await storage.TakePendingAsync(stateValue) is not { } pending)
        {
            throw new SmartException(SmartFailure.StateMismatch);
        }

        if (response.TryGetValue("error", out var error))
        {
            throw new SmartException(error == "access_denied" ? SmartFailure.AuthorizationDenied : SmartFailure.TokenExchangeFailed);
        }

        var server = new PendingServer(pending);
        var client = await CreateClientAsync(server, pending.RedirectUri, pending.Scope, cancellationToken);
        var authorizeState = new AuthorizeState { State = pending.State, CodeVerifier = pending.CodeVerifier, RedirectUri = pending.RedirectUri };
        var result = await client.ProcessResponseAsync(callbackUrl, authorizeState, null, cancellationToken);
        if (result.IsError || string.IsNullOrEmpty(result.AccessToken))
        {
            throw new SmartException(SmartFailure.TokenExchangeFailed, result.Error);
        }

        var session = new SmartSession(result.AccessToken, result.AccessTokenExpiration, Field(result, PatientField), Field(result, EncounterField),
                                       result.User?.FindFirst(FhirUserClaim)?.Value, result.TokenResponse?.Scope ?? string.Empty);
        credentials.SetAccessToken(server, session.AccessToken, session.ExpiresAt);
        await storage.SaveSessionAsync(server.Key, server.BaseUrl, session);
        if (pending.Launched)
        {
            // A launch from outside reads the server at the address of the launch in this tab
            await storage.SaveIssAsync(server.Key, server.BaseUrl);
        }

        return new SmartCompletion(server.Key, pending.ReturnUrl, session.PatientId);
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreAsync(IViewerServer server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.AuthenticationType != AuthenticationType.OAuth2)
        {
            return false;
        }

        if (credentials.Has(server))
        {
            return true;
        }

        if (await storage.LoadSessionAsync(server.Key, server.BaseUrl) is not { } session)
        {
            return false;
        }

        credentials.SetAccessToken(server, session.AccessToken, session.ExpiresAt);
        if (credentials.Has(server))
        {
            return true;
        }

        credentials.Forget(server);
        await storage.RemoveSessionAsync(server.Key);
        return false;
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(IViewerServer server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        credentials.Forget(server);
        if (server.AuthenticationType == AuthenticationType.OAuth2)
        {
            await storage.RemoveSessionAsync(server.Key);
        }
    }

    private async Task<OidcClient> CreateClientAsync(IViewerServer server, string redirectUri, string scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(server.OAuth2ClientId) || string.IsNullOrWhiteSpace(server.OAuth2Scope))
        {
            throw new SmartException(SmartFailure.ClientNotConfigured);
        }

        var configuration = await DiscoverAsync(server, cancellationToken);
        if (!configuration.AllowsPkce)
        {
            throw new SmartException(SmartFailure.PkceNotSupported);
        }

        var options = new OidcClientOptions
        {
            ClientId = server.OAuth2ClientId,
            Scope = scope,
            RedirectUri = redirectUri,
            LoadProfile = false,
            FilterClaims = false,
            HttpClientFactory = _ => httpClientFactory.CreateClient(FhirServerHttp.HttpClientName),
            ProviderInformation = new ProviderInformation
            {
                IssuerName = string.IsNullOrWhiteSpace(server.Authority) ? server.BaseUrl : server.Authority,
                AuthorizeEndpoint = configuration.AuthorizationEndpoint,
                TokenEndpoint = configuration.TokenEndpoint,
                KeySet = new JsonWebKeySet()
            },
            // The id token is not the point of a SMART sign-in: its signature is not checked here, fhirUser is only shown
            Policy = new Policy { RequireIdentityTokenSignature = false, RequireAccessTokenHash = false, ValidateTokenIssuerName = false }
        };
        return new OidcClient(options);
    }

    // An EHR launch needs the scope "launch" whatever the server says
    private static string ScopeOf(IViewerServer server, string? launch)
    {
        var scope = server.OAuth2Scope ?? string.Empty;
        return string.IsNullOrEmpty(launch) || scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("launch", StringComparer.Ordinal) ? scope : $"launch {scope}".Trim();
    }

    // The document of the authorization server: of the authority when the server has one, else of the FHIR server (SMART)
    private async Task<SmartConfiguration> DiscoverAsync(IViewerServer server, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(server.Authority)
            ? SmartConfiguration.UrlOf(server.BaseUrl)
            : $"{server.Authority.TrimEnd('/')}/.well-known/openid-configuration";
        try
        {
            using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, url, null, cancellationToken);
            return SmartConfiguration.Parse(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            throw new SmartException(SmartFailure.DiscoveryFailed, ex.Message);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtraParametersOf(IViewerServer server)
    {
        return (server.OAuth2ExtraParameters ?? string.Empty)
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => KeyValuePair.Create(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1])));
    }

    private static Dictionary<string, string> QueryOf(string url)
    {
        return new Uri(url).Query
                           .TrimStart('?')
                           .Split('&', StringSplitOptions.RemoveEmptyEntries)
                           .Select(pair => pair.Split('=', 2))
                           .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
                           .ToDictionary(group => group.Key, group => group.First() is { Length: 2 } parts ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty, StringComparer.Ordinal);
    }

    // patient and encounter come in the token response next to the token
    private static string? Field(LoginResult result, string name)
    {
        return result.TokenResponse?.Json is { } json && json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // The server as the pending sign-in remembers it: what the way back needs, no registry is asked
    private sealed class PendingServer(SmartPending pending) : IViewerServer
    {
        public string Key => pending.ServerKey;

        public string Name => pending.Name;

        public string BaseUrl => pending.BaseUrl;

        public AuthenticationType AuthenticationType => AuthenticationType.OAuth2;

        public FhirRelease? FhirRelease => null;

        public int PageSize => 0;

        public string? Authority => pending.Authority;

        public string? OAuth2ClientId => pending.ClientId;

        public string? OAuth2Scope => pending.Scope;

        public string? OAuth2ExtraParameters => null;
    }
}
