namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// How a FHIR server wants its requests to be authorized.
/// </summary>
/// <example>
/// <code>
/// var needsSecret = server.AuthenticationType is AuthenticationType.Basic or AuthenticationType.Token;
/// </code>
/// </example>
public enum AuthenticationType
{
    /// <summary>A public server: no <c>Authorization</c> header.</summary>
    None,

    /// <summary>A user name and a password (<c>Authorization: Basic</c>), asked for when the server is opened and kept in memory only.</summary>
    Basic,

    /// <summary>A sign-in by SMART App Launch (OAuth2 with PKCE); the access token is the <c>Bearer</c> of the requests.</summary>
    OAuth2,

    /// <summary>A token the user pastes (<c>Authorization: Bearer</c>), kept in memory only.</summary>
    Token
}
