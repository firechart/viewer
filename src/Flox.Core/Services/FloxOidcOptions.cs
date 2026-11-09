namespace Flox.Core.Services;

// Explicit opt-in for OIDC/OAuth2, bound once from the "Flox:Oidc" config section by FloxBootstrapper
// and shared as a singleton by every host (Server/WASM/MAUI) and by Razor components (the main menu,
// ProfileWidget) that need to hide Login/Logout/Profile when there's no identity provider to use.
// Enabled defaults to false - presence of Authority/ClientId no longer implies "on", since a typo
// in a key name would silently disable auth instead of failing loudly.
//
// Everything that differs between identity providers is configuration, so switching provider
// never needs a rebuild. The defaults below suit Orchard Core's own OpenID server, the provider Flox servers run.
public class FloxOidcOptions
{
    // "openid" is always requested and need not be listed
    public static readonly IReadOnlyList<string> DefaultScopes = ["profile", "email", "roles", "offline_access"];

    // Where roles live in a token: a claim name ("roles", "groups", "cognito:groups", a URL-named
    // claim) or a dotted path into a JSON claim; {clientId} is replaced by ClientId
    public static readonly IReadOnlyList<string> DefaultRoleClaims = ["role"];

    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }

    // Null means DefaultScopes. Arrays are replaced, not merged, by configuration binding, which is
    // why the defaults are not pre-filled here
    public string[]? Scopes { get; set; }

    // Audience the server requires in access tokens; null means ClientId
    public string? Audience { get; set; }

    // Whether the server checks the token's audience claim against EffectiveAudience. Defaults to
    // false - Orchard's access tokens carry aud=oct:<tenant>, not the client_id, so requiring it
    // out of the box breaks a stock setup. Server-only; WASM/MAUI clients never validate access tokens.
    public bool ValidateAudience { get; set; }

    // Extra parameters for the authorization request, e.g. { "audience": "https://api.example.com" }
    // which Auth0 needs to issue a JWT access token
    public Dictionary<string, string>? AdditionalParameters { get; }

    public string NameClaim { get; set; } = Constants.NAME_CLAIM;

    // Null means DefaultRoleClaims
    public string[]? RoleClaims { get; set; }

    public IReadOnlyList<string> EffectiveScopes
    {
        get
        {
            return Scopes is { Length: > 0 } ? Scopes : DefaultScopes;
        }
    }

    public IReadOnlyList<string> EffectiveRoleClaims
    {
        get
        {
            return RoleClaims is { Length: > 0 } ? RoleClaims : DefaultRoleClaims;
        }
    }

    public string? EffectiveAudience
    {
        get
        {
            return string.IsNullOrEmpty(Audience) ? ClientId : Audience;
        }
    }

    // Space-separated scope string for libraries that take one
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

    // MAUI-only: custom URL scheme registered in AndroidManifest.xml/Info.plist to receive the
    // OIDC callback on Android/iOS/MacCatalyst (Windows uses a loopback listener instead - see
    // MauiOAuth2AuthenticationStateProvider). Defaults to Flox's own scheme; every other app built
    // on Flox must override this with its own registered scheme.
    public string MauiRedirectScheme { get; set; } = "com.flox";

    // MAUI on Windows: the port of the loopback listener that receives the login callback. Change it when
    // the default is taken on the workstation (another program, a reserved range, a shared terminal server)
    public int MauiLoopbackPort { get; set; } = 7890;

    public string MauiLoopbackRedirectUri
    {
        get
        {
            return $"http://127.0.0.1:{MauiLoopbackPort}/callback";
        }
    }

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
