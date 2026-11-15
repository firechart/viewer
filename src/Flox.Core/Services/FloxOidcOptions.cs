namespace Flox.Core.Services;

/// <summary>Settings of the OpenID Connect sign-in, bound once from the <c>Flox:Oidc</c> section and shared as a singleton by every host and by the components that hide Login, Logout and Profile when there is no identity provider. Sign-in is on only when <see cref="Enabled"/> is set: the presence of the authority and the client id does not imply it, so a typo in a key name fails loudly instead of silently disabling it. Everything that differs between identity providers is configuration; the defaults suit the OpenID server of Orchard Core.</summary>
public class FloxOidcOptions
{
    /// <summary>The scopes requested when none are configured; <c>openid</c> is always requested and need not be listed.</summary>
    public static readonly IReadOnlyList<string> DefaultScopes = ["profile", "email", "roles", "offline_access"];

    /// <summary>The claims that hold roles when none are configured: a claim name or a dotted path into a JSON claim; <c>{clientId}</c> is replaced by <see cref="ClientId"/>.</summary>
    public static readonly IReadOnlyList<string> DefaultRoleClaims = ["role"];

    /// <summary>Whether sign-in is on.</summary>
    public bool Enabled { get; set; }
    /// <summary>The address of the identity provider.</summary>
    public string? Authority { get; set; }
    /// <summary>The id of the public client.</summary>
    public string? ClientId { get; set; }

    /// <summary>The scopes to request; null means <see cref="DefaultScopes"/>. Arrays are replaced, not merged, by configuration binding, which is why the defaults are not pre-filled.</summary>
    public string[]? Scopes { get; set; }

    /// <summary>The audience the server requires in access tokens; null means <see cref="ClientId"/>.</summary>
    public string? Audience { get; set; }

    /// <summary>Whether the server checks the audience of a token against <see cref="EffectiveAudience"/>; off by default because the access tokens of Orchard carry another audience than the client id. Server only.</summary>
    public bool ValidateAudience { get; set; }

    /// <summary>Extra parameters of the authorization request, for providers that need them to issue a JWT access token.</summary>
    public Dictionary<string, string>? AdditionalParameters { get; }

    /// <summary>The claim that holds the display name of the user.</summary>
    public string NameClaim { get; set; } = Constants.NAME_CLAIM;

    /// <summary>The claims that hold roles; null means <see cref="DefaultRoleClaims"/>.</summary>
    public string[]? RoleClaims { get; set; }

    /// <summary>The scopes in effect: the configured ones or the defaults.</summary>
    public IReadOnlyList<string> EffectiveScopes
    {
        get
        {
            return Scopes is { Length: > 0 } ? Scopes : DefaultScopes;
        }
    }

    /// <summary>The role claims in effect: the configured ones or the defaults.</summary>
    public IReadOnlyList<string> EffectiveRoleClaims
    {
        get
        {
            return RoleClaims is { Length: > 0 } ? RoleClaims : DefaultRoleClaims;
        }
    }

    /// <summary>The audience in effect: the configured one or the client id.</summary>
    public string? EffectiveAudience
    {
        get
        {
            return string.IsNullOrEmpty(Audience) ? ClientId : Audience;
        }
    }

    /// <summary>The scopes as one space-separated string, <c>openid</c> first, for libraries that take one.</summary>
    public string ScopeString
    {
        get
        {
            return string.Join(' ',
                               new[]
                                   {
                                       "openid"
                                   }.Concat(EffectiveScopes)
                                    .Distinct());
        }
    }

    /// <summary>MAUI only: the custom URL scheme registered in the application manifest to receive the sign-in callback on Android, iOS and MacCatalyst. Every application built on Flox overrides it with its own scheme.</summary>
    public string MauiRedirectScheme { get; set; } = "com.flox";

    /// <summary>MAUI on Windows: the port of the loopback listener that receives the sign-in callback; change it when the default is taken on the workstation.</summary>
    public int MauiLoopbackPort { get; set; } = 7890;

    /// <summary>MAUI on Windows: the address the identity provider redirects to after sign-in.</summary>
    public string MauiLoopbackRedirectUri
    {
        get
        {
            return $"http://127.0.0.1:{MauiLoopbackPort}/callback";
        }
    }

    /// <summary>Checks that the options are complete when sign-in is on.</summary>
    /// <exception cref="InvalidOperationException">The authority or the client id is missing, or the loopback port is out of range.</exception>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(Authority) || string.IsNullOrEmpty(ClientId))
        {
            throw new InvalidOperationException("Flox:Oidc:Enabled is true but Flox:Oidc:Authority/Flox:Oidc:ClientId are missing. " + "Either configure both or set Flox:Oidc:Enabled=false.");
        }

        if (MauiLoopbackPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException("Flox:Oidc:MauiLoopbackPort must be between 1024 and 65535.");
        }
    }
}
