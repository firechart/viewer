using System.Text.Json;

namespace FireChart.Viewer.Core.Smart;

/// <summary>
/// The SMART discovery document of a server (<c>.well-known/smart-configuration</c>), or the OpenID configuration of its authority.
/// </summary>
/// <param name="AuthorizationEndpoint">Where the user signs in.</param>
/// <param name="TokenEndpoint">Where the code is exchanged for a token.</param>
/// <param name="RevocationEndpoint">Where a token is revoked, or <see langword="null"/>.</param>
/// <param name="Capabilities">The SMART capabilities (<c>launch-ehr</c>, <c>client-public</c>, ...).</param>
/// <param name="CodeChallengeMethods">The PKCE methods the server takes; empty when it does not say.</param>
/// <example>
/// <code>
/// using var document = JsonDocument.Parse(json);
/// var configuration = SmartConfiguration.Parse(document.RootElement);
/// var standalone = configuration.Supports("launch-standalone");
/// </code>
/// </example>
public sealed record SmartConfiguration(
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string? RevocationEndpoint,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> CodeChallengeMethods)
{
    /// <summary>The path of the discovery document below the base of a FHIR server.</summary>
    public const string WellKnownPath = ".well-known/smart-configuration";

    /// <summary>The PKCE method used: <c>S256</c>.</summary>
    public const string ChallengeMethod = "S256";

    /// <summary>
    /// The server declares a capability.
    /// </summary>
    /// <param name="capability">The capability (<c>launch-ehr</c>).</param>
    /// <returns><see langword="true"/> when it is in <see cref="Capabilities"/>.</returns>
    public bool Supports(string capability)
    {
        return Capabilities.Contains(capability, StringComparer.Ordinal);
    }

    /// <summary>
    /// PKCE with S256 can be used: the weaker <c>plain</c> never is; a server that does not list the methods is trusted to take S256.
    /// </summary>
    public bool AllowsPkce => CodeChallengeMethods.Count == 0 || CodeChallengeMethods.Contains(ChallengeMethod, StringComparer.Ordinal);

    /// <summary>
    /// Reads a discovery document.
    /// </summary>
    /// <param name="root">The JSON of the document.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="SmartException">The document has no authorization or token endpoint (<see cref="SmartFailure.DiscoveryFailed"/>).</exception>
    public static SmartConfiguration Parse(JsonElement root)
    {
        var authorization = Text(root, "authorization_endpoint");
        var token = Text(root, "token_endpoint");
        if (authorization is null || token is null)
        {
            throw new SmartException(SmartFailure.DiscoveryFailed);
        }

        return new SmartConfiguration(authorization, token, Text(root, "revocation_endpoint"), List(root, "capabilities"), List(root, "code_challenge_methods_supported"));
    }

    /// <summary>
    /// Where the discovery document of a server is.
    /// </summary>
    /// <param name="baseUrl">The base address of the FHIR server.</param>
    /// <returns>The address of the document.</returns>
    public static string UrlOf(string baseUrl)
    {
        return $"{baseUrl.TrimEnd('/')}/{WellKnownPath}";
    }

    private static string? Text(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
    }

    private static string[] List(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.String).Select(row => row.GetString()!)]
            : [];
    }
}

/// <summary>
/// Why a SMART sign-in failed.
/// </summary>
public enum SmartFailure
{
    /// <summary>The discovery document could not be read.</summary>
    DiscoveryFailed,

    /// <summary>The server does not take PKCE with S256.</summary>
    PkceNotSupported,

    /// <summary>The way back does not belong to a sign-in of this tab (another tab, a reused or forged state).</summary>
    StateMismatch,

    /// <summary>The user or the server refused the authorization.</summary>
    AuthorizationDenied,

    /// <summary>The code could not be exchanged for a token.</summary>
    TokenExchangeFailed,

    /// <summary>An EHR launch named a server the host does not know.</summary>
    UnknownIss,

    /// <summary>The server has no client id or scope for SMART.</summary>
    ClientNotConfigured
}

/// <summary>
/// A SMART sign-in failed.
/// </summary>
/// <param name="failure">Why.</param>
/// <param name="detail">The detail for the log, in English; the kind of the failure by default.</param>
/// <example>
/// <code>
/// catch (SmartException exception)
/// {
///     message = exception.Failure.ToMessage();
/// }
/// </code>
/// </example>
public sealed class SmartException(SmartFailure failure, string? detail = null) : Exception(detail ?? failure.ToString())
{
    /// <summary>Why the sign-in failed.</summary>
    public SmartFailure Failure { get; } = failure;
}
