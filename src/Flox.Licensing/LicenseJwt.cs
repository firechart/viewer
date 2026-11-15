using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flox.Licensing;

/// <summary>
/// A decoded license token whose signature is not checked yet.
/// </summary>
/// <param name="Header">The JOSE header.</param>
/// <param name="Payload">The claims.</param>
/// <param name="SigningInput">The encoded header and payload, the bytes the signature covers.</param>
/// <param name="Signature">The raw signature.</param>
public sealed record LicenseToken(JsonObject Header, JsonObject Payload, string SigningInput, ReadOnlyMemory<byte> Signature)
{
    /// <summary>The identifier of the key the token names in its header, or null.</summary>
    public string? KeyId
    {
        get
        {
            return LicenseJwt.Text(Header, "kid");
        }
    }
}

/// <summary>
/// The JWT form of a license: JWS compact serialization with ES256 (ECDsa P-256 signs in the IEEE P1363 format that JWS expects).
/// </summary>
public static class LicenseJwt
{
    /// <summary>The issuer claim every license carries.</summary>
    public const string Issuer = "https://firechart.org/licensing";
    /// <summary>The token type in the header.</summary>
    public const string TokenType = "license+jwt";
    /// <summary>The signature algorithm in the header.</summary>
    public const string Algorithm = "ES256";

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>Builds the JOSE header for a key.</summary>
    public static JsonObject Header(string keyId)
    {
        return new JsonObject
        {
            ["alg"] = Algorithm,
            ["typ"] = TokenType,
            ["kid"] = keyId,
        };
    }

    /// <summary>Builds the claims of a license.</summary>
    public static JsonObject Payload(License license)
    {
        var payload = new JsonObject
        {
            ["iss"] = Issuer,
            ["jti"] = license.Id,
            ["sub"] = license.Customer,
            ["name"] = license.Name,
            ["aud"] = Array(license.Audience),
            ["iat"] = license.IssuedAt.ToUnixTimeSeconds(),
            ["nbf"] = license.Starts.ToUnixTimeSeconds(),
        };
        if (license.Ends is { } ends)
        {
            payload["exp"] = ends.ToUnixTimeSeconds();
        }
        payload["edition"] = license.Edition;
        payload["kind"] = LicenseJson.KindName(license.Kind);
        if (license.Features.Count > 0)
        {
            payload["features"] = Array(license.Features);
        }
        if (license.Limits.Count > 0)
        {
            payload["limits"] = new JsonObject(license.Limits.Select(l => KeyValuePair.Create(l.Key, (JsonNode?)JsonValue.Create(l.Value))));
        }
        payload["support_until"] = license.SupportUntil.ToString(DayFormat, CultureInfo.InvariantCulture);
        return payload;
    }

    /// <summary>Signs a license; the key signs as <see cref="License.KeyId"/>, the generator checks that they belong together.</summary>
    public static string Sign(License license, ECDsa privateKey)
    {
        var input = Part(Header(license.KeyId)) + "." + Part(Payload(license));
        var signature = privateKey.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256);
        return input + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>Splits and decodes a compact JWT without checking its signature.</summary>
    /// <exception cref="LicenseException">The text is not a JWT.</exception>
    public static LicenseToken Decode(string token)
    {
        var parts = token.Trim().Split('.');
        if (parts.Length != 3)
        {
            throw new LicenseException("Not a JWT: three parts separated by dots are expected");
        }
        try
        {
            return new LicenseToken(Object(parts[0]), Object(parts[1]), parts[0] + "." + parts[1], Base64Url.DecodeFromChars(parts[2]));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            throw new LicenseException("A broken JWT: " + e.Message);
        }
    }

    /// <summary>Checks the signature only; the issuer, the type and the site are checked by <see cref="LicenseVerifier"/>.</summary>
    public static bool VerifySignature(LicenseToken token, string publicKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        return key.KeySize == 256
            && Text(token.Header, "alg") == Algorithm
            && key.VerifyData(Encoding.ASCII.GetBytes(token.SigningInput), token.Signature.Span, HashAlgorithmName.SHA256);
    }

    /// <summary>Reads the claims of a token into a license.</summary>
    /// <exception cref="LicenseException">A required claim is missing or the kind is unknown.</exception>
    public static License ToLicense(LicenseToken token)
    {
        var payload = token.Payload;
        var id = Text(payload, "jti");
        var customer = Text(payload, "sub");
        var name = Text(payload, "name");
        var edition = Text(payload, "edition");
        var issuedAt = Number(payload, "iat");
        var notBefore = Number(payload, "nbf");
        var support = Text(payload, "support_until");
        if (id is null || customer is null || name is null || edition is null || issuedAt is null || notBefore is null
            || !DateOnly.TryParseExact(support, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var supportUntil))
        {
            throw new LicenseException("The license lacks a required claim (jti, sub, name, edition, iat, nbf, support_until)");
        }

        var kindText = Text(payload, "kind");
        var kind = LicenseKind.Subscription;
        if (kindText is not null && (!Enum.TryParse(kindText, ignoreCase: true, out kind) || kind == LicenseKind.Free))
        {
            throw new LicenseException("An unknown kind of license: " + kindText);
        }

        var limits = new Dictionary<string, long>(StringComparer.Ordinal);
        if (payload["limits"] is JsonObject limitObject)
        {
            foreach (var (limitName, value) in limitObject)
            {
                if (value is JsonValue number && number.TryGetValue<long>(out var limit))
                {
                    limits[limitName] = limit;
                }
            }
        }

        var expires = Number(payload, "exp");
        return new License
        {
            Id = id,
            Customer = customer,
            Name = name,
            Audience = Strings(payload["aud"]),
            Edition = edition,
            Kind = kind,
            Features = Strings(payload["features"]),
            Limits = limits,
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAt.Value),
            ValidFrom = DayOf(notBefore.Value),
            ValidUntil = expires is { } exp ? DayOf(exp - 1) : null,
            SupportUntil = supportUntil,
            KeyId = token.KeyId ?? "",
        };
    }

    /// <summary>Reads a non-empty string claim, or null when it is absent or not a string.</summary>
    public static string? Text(JsonObject json, string name)
    {
        return json[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
    }

    private static long? Number(JsonObject json, string name)
    {
        return json[name] is JsonValue value && value.TryGetValue<long>(out var number) ? number : null;
    }

    private static List<string> Strings(JsonNode? node)
    {
        return node switch
        {
            JsonArray array => [.. array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()],
            JsonValue value when value.TryGetValue<string>(out var single) => [single],
            _ => [],
        };
    }

    private static DateOnly DayOf(long seconds)
    {
        return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);
    }

    private static JsonArray Array(IEnumerable<string> values)
    {
        return new JsonArray([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);
    }

    private static string Part(JsonObject value)
    {
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value.ToJsonString(LicenseJson.Compact)));
    }

    private static JsonObject Object(string part)
    {
        return JsonNode.Parse(Base64Url.DecodeFromChars(part)) as JsonObject ?? throw new FormatException("not a JSON object");
    }
}
