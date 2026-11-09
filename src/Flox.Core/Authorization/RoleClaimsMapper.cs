using System.Security.Claims;
using System.Text.Json;
using Flox.Core.Services;

namespace Flox.Core.Authorization;

// Providers put roles in different places: Orchard uses "role", Entra "roles", Okta "groups",
// Cognito "cognito:groups", Auth0 a URL-named claim, some nest them (realm_access.roles). Which places to read is configuration (FloxOidcOptions.RoleClaims);
// this maps them all to ClaimTypes.Role, the only role claim type the application uses.
//
// A path is either the exact name of a claim, or a dotted walk into a claim holding a JSON object.
// Claim and property names may themselves contain dots, so every split is tried. Roles are only
// taken from the configured paths (put your own client in the path, never "all clients"), so a
// role of another application cannot grant rights here. Idempotent, never throws on bad input.
public static class RoleClaimsMapper
{
    private const string ClientIdPlaceholder = "{clientId}";

    public static void Apply(ClaimsIdentity identity, FloxOidcOptions oidc)
    {
        ArgumentNullException.ThrowIfNull(oidc);
        Apply(identity, oidc.EffectiveRoleClaims, oidc.ClientId);
    }

    public static void Apply(ClaimsIdentity identity, IEnumerable<string> paths, string? clientId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(paths);

        var claims = identity.Claims.ToList();
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segments in Resolve(paths, clientId))
        {
            CollectFromClaims(claims, segments, roles);
        }

        foreach (var role in roles.Where(row => !string.IsNullOrWhiteSpace(row)))
        {
            if (!identity.HasClaim(ClaimTypes.Role, role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }
    }

    // For clients that only see the ID token while providers put roles into the access token: copies
    // the claims the configured paths start from out of the access token payload, then maps them.
    // The payload is read without validation (the server validates the token); it only feeds UI hints
    public static void ApplyAccessToken(ClaimsIdentity identity, string? accessToken, FloxOidcOptions oidc)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(oidc);

        if (TryReadPayload(accessToken) is { ValueKind: JsonValueKind.Object } payload)
        {
            foreach (var segments in Resolve(oidc.EffectiveRoleClaims, oidc.ClientId))
            {
                for (var length = 1; length <= segments.Length; length++)
                {
                    var name = string.Join('.', segments.Take(length));
                    if (payload.TryGetProperty(name, out var value) && !identity.HasClaim(row => row.Type == name))
                    {
                        AddPayloadClaim(identity, name, value);
                    }
                }
            }
        }

        Apply(identity, oidc);
    }

    private static IEnumerable<string[]> Resolve(IEnumerable<string> paths, string? clientId)
    {
        foreach (var path in paths.Where(row => !string.IsNullOrWhiteSpace(row)))
        {
            if (path.Contains(ClientIdPlaceholder, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(clientId))
                {
                    continue;
                }

                yield return path.Replace(ClientIdPlaceholder, clientId, StringComparison.Ordinal)
                                 .Split('.');
            }
            else
            {
                yield return path.Split('.');
            }
        }
    }

    private static void CollectFromClaims(List<Claim> claims, string[] segments, HashSet<string> roles)
    {
        for (var length = 1; length <= segments.Length; length++)
        {
            var name = string.Join('.', segments.Take(length));
            foreach (var claim in claims.Where(row => row.Type == name))
            {
                if (length == segments.Length)
                {
                    CollectValue(claim.Value, roles);
                }
                else if (TryParse(claim.Value) is { ValueKind: JsonValueKind.Object } root)
                {
                    Navigate(root, segments, length, roles);
                }
            }
        }
    }

    private static void Navigate(JsonElement element, string[] segments, int index, HashSet<string> roles)
    {
        if (index == segments.Length)
        {
            CollectElement(element, roles);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        for (var length = 1; length <= segments.Length - index; length++)
        {
            var name = string.Join('.',
                                   segments.Skip(index)
                                           .Take(length));
            if (element.TryGetProperty(name, out var child))
            {
                Navigate(child, segments, index + length, roles);
            }
        }
    }

    // A claim value is a role, or a JSON array of roles
    private static void CollectValue(string value, HashSet<string> roles)
    {
        if (TryParse(value) is { ValueKind: JsonValueKind.Array } array)
        {
            CollectElement(array, roles);
        }
        else
        {
            roles.Add(value);
        }
    }

    private static void CollectElement(JsonElement element, HashSet<string> roles)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                roles.Add(element.GetString()!);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()
                                            .Where(row => row.ValueKind == JsonValueKind.String))
                {
                    roles.Add(item.GetString()!);
                }

                break;
        }
    }

    private static void AddPayloadClaim(ClaimsIdentity identity, string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                identity.AddClaim(new Claim(name, value.GetRawText()));
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()
                                          .Where(row => row.ValueKind == JsonValueKind.String))
                {
                    identity.AddClaim(new Claim(name, item.GetString()!));
                }

                break;
            case JsonValueKind.String:
                identity.AddClaim(new Claim(name, value.GetString()!));
                break;
        }
    }

    private static JsonElement? TryParse(string value)
    {
        var trimmed = value.AsSpan()
                           .TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? TryReadPayload(string? jwt)
    {
        var parts = jwt?.Split('.');
        if (parts is not { Length: >= 2 } || parts[1].Length == 0)
        {
            return null;
        }

        try
        {
            var base64 = parts[1]
                        .Replace('-', '+')
                        .Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(base64));
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
