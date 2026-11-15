using System.Security.Cryptography;

namespace Flox.Licensing;

/// <summary>
/// A public key of the vendor as the generator writes it (keys/&lt;kid&gt;.pem). Products keep them in code, never in configuration.
/// </summary>
/// <param name="Id">The key identifier a license names as its <c>kid</c>.</param>
/// <param name="Pem">The key in PEM form.</param>
public sealed record LicensePublicKey(string Id, string Pem);

/// <summary>
/// Checks a license token against the public keys of the vendor and the address of the site.
/// </summary>
public static class LicenseVerifier
{
    /// <summary>How long before the end a license counts as expiring.</summary>
    public static readonly TimeSpan WarningPeriod = TimeSpan.FromDays(30);
    /// <summary>How long after the end a license keeps working.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    /// <summary>Checks a token; an empty token is no license, never an error.</summary>
    /// <param name="token">The compact JWT, or null.</param>
    /// <param name="audience">The public address of the site (Flox:Oidc:Authority).</param>
    /// <param name="keys">The public keys of the vendor.</param>
    /// <param name="now">The moment to judge the license at.</param>
    public static LicenseStatus Verify(string? token, string audience, IEnumerable<LicensePublicKey> keys, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return LicenseStatus.NoLicense;
        }
        try
        {
            var decoded = LicenseJwt.Decode(token);
            var key = keys.FirstOrDefault(k => k.Id == decoded.KeyId);
            if (key is null)
            {
                return Invalid("The license is signed with an unknown key: " + decoded.KeyId);
            }
            if (!LicenseJwt.VerifySignature(decoded, key.Pem))
            {
                return Invalid("The signature of the license does not match the key " + key.Id);
            }
            if (LicenseJwt.Text(decoded.Header, "typ") != LicenseJwt.TokenType || LicenseJwt.Text(decoded.Payload, "iss") != LicenseJwt.Issuer)
            {
                return Invalid("Not a license: the type or the issuer differ");
            }
            var license = LicenseJwt.ToLicense(decoded);
            if (!license.Audience.Contains(audience.TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
            {
                return Invalid("The license is for another site: " + string.Join(", ", license.Audience));
            }
            return new LicenseStatus(StateAt(license, now), license, null);
        }
        catch (Exception e) when (e is LicenseException or ArgumentException or CryptographicException)
        {
            return Invalid(e.Message);
        }
    }

    /// <summary>Checks the token in a file; a missing file is no license, a file that cannot be read is an invalid one.</summary>
    /// <param name="path">The license file.</param>
    /// <param name="audience">The public address of the site (Flox:Oidc:Authority).</param>
    /// <param name="keys">The public keys of the vendor.</param>
    /// <param name="now">The moment to judge the license at.</param>
    public static LicenseStatus VerifyFile(string path, string audience, IEnumerable<LicensePublicKey> keys, DateTimeOffset now)
    {
        if (!File.Exists(path))
        {
            return LicenseStatus.NoLicense;
        }
        try
        {
            return Verify(File.ReadAllText(path), audience, keys, now);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Invalid("The license file cannot be read: " + e.Message);
        }
    }

    /// <summary>Gets the state of a license, whose signature is already checked, at a moment.</summary>
    public static LicenseState StateAt(License license, DateTimeOffset now)
    {
        if (now < license.Starts)
        {
            return LicenseState.NotYetValid;
        }
        if (license.Ends is not { } end || now < end - WarningPeriod)
        {
            return LicenseState.Valid;
        }
        if (now < end)
        {
            return LicenseState.Expiring;
        }
        return now < end + GracePeriod ? LicenseState.Grace : LicenseState.Expired;
    }

    private static LicenseStatus Invalid(string error)
    {
        return new LicenseStatus(LicenseState.Invalid, null, error);
    }
}
