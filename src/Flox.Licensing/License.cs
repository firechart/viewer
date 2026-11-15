using System.Text.Json.Serialization;

namespace Flox.Licensing;

/// <summary>
/// The commercial kind of a license.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LicenseKind>))]
public enum LicenseKind
{
    /// <summary>A license for a period that is renewed.</summary>
    Subscription,

    /// <summary>A license for a trial installation at a customer.</summary>
    Pilot,

    /// <summary>A license without an end date.</summary>
    Perpetual,

    /// <summary>A license for testing by the vendor.</summary>
    Test,

    /// <summary>Only <see cref="License.Free"/>, never signed: the generator refuses it, the verifier rejects it.</summary>
    Free,
}

/// <summary>
/// What a license grants: the JWT carries exactly these fields. The generator keeps a record with more (partner, price, comment).
/// </summary>
public record License
{
    /// <summary>The edition name of <see cref="Free"/>.</summary>
    public const string FreeEdition = "Free";

    /// <summary>What <see cref="ILicenseService.GetLicense"/> gives without a license in force: no features, no limits. Its real state is in <see cref="LicenseStatus"/>.</summary>
    public static readonly License Free = new()
    {
        Id = "free",
        Customer = "",
        Name = "",
        Audience = [],
        Edition = FreeEdition,
        Kind = LicenseKind.Free,
        IssuedAt = DateTimeOffset.UnixEpoch,
        ValidFrom = DateOnly.MinValue,
        SupportUntil = DateOnly.MinValue,
        KeyId = "",
    };

    /// <summary>The unique identifier of the license.</summary>
    public required string Id { get; init; }
    /// <summary>The customer the license was issued to.</summary>
    public required string Customer { get; init; }
    /// <summary>The display name of the license.</summary>
    public required string Name { get; init; }

    /// <summary>The public addresses of the sites (Flox:Oidc:Authority).</summary>
    public required IReadOnlyList<string> Audience { get; init; }

    /// <summary>The edition of the product the license grants.</summary>
    public required string Edition { get; init; }
    /// <summary>The commercial kind of the license.</summary>
    public required LicenseKind Kind { get; init; }

    /// <summary>Arbitrary feature names; what they mean is the product's business.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary>Named numeric limits the license sets.</summary>
    public IReadOnlyDictionary<string, long> Limits { get; init; } = new Dictionary<string, long>();
    /// <summary>The moment the license was issued.</summary>
    public required DateTimeOffset IssuedAt { get; init; }
    /// <summary>The first day of validity.</summary>
    public required DateOnly ValidFrom { get; init; }

    /// <summary>The last day of validity, inclusive; null for a perpetual license.</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>The last day of support.</summary>
    public required DateOnly SupportUntil { get; init; }
    /// <summary>The identifier of the vendor key that signed the license.</summary>
    public required string KeyId { get; init; }

    /// <summary>The moment the license starts (the beginning of <see cref="ValidFrom"/>, UTC).</summary>
    [JsonIgnore]
    public DateTimeOffset Starts
    {
        get
        {
            return StartOf(ValidFrom);
        }
    }

    /// <summary>The moment after the last day; null for a perpetual license.</summary>
    [JsonIgnore]
    public DateTimeOffset? Ends
    {
        get
        {
            return ValidUntil is { } until ? StartOf(until.AddDays(1)) : null;
        }
    }

    /// <summary>Whether this is the free edition, not a signed license.</summary>
    [JsonIgnore]
    public bool IsFree
    {
        get
        {
            return Kind == LicenseKind.Free;
        }
    }

    /// <summary>Tells whether the license lists the feature.</summary>
    public bool HasFeature(string feature)
    {
        return Features.Contains(feature, StringComparer.Ordinal);
    }

    /// <summary>Gets the named limit, or null when the license sets none.</summary>
    public long? Limit(string name)
    {
        return Limits.TryGetValue(name, out var value) ? value : null;
    }

    internal static DateTimeOffset StartOf(DateOnly day)
    {
        return new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}

/// <summary>
/// The state of a license at a moment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LicenseState>))]
public enum LicenseState
{
    /// <summary>No license: the free edition, no warnings.</summary>
    None,

    /// <summary>The license is in force.</summary>
    Valid,

    /// <summary>Less than <see cref="LicenseVerifier.WarningPeriod"/> before the end.</summary>
    Expiring,

    /// <summary>Ended less than <see cref="LicenseVerifier.GracePeriod"/> ago: everything works as before.</summary>
    Grace,

    /// <summary>The grace period is over.</summary>
    Expired,

    /// <summary>Starts later.</summary>
    NotYetValid,

    /// <summary>The signature, the key, the issuer, the type or the site do not match, or a field is missing.</summary>
    Invalid,
}

/// <summary>
/// The state of the license at a moment with the license itself and, when it is invalid, the reason.
/// </summary>
/// <param name="State">The state of the license.</param>
/// <param name="License">The license, or null when there is none or it could not be read.</param>
/// <param name="Error">The reason the license is invalid, or null.</param>
public sealed record LicenseStatus(LicenseState State, License? License, string? Error)
{
    /// <summary>The status of a site without a license.</summary>
    public static readonly LicenseStatus NoLicense = new(LicenseState.None, null, null);

    /// <summary>Whether the license grants its edition and features (also while expiring and during the grace period).</summary>
    public bool IsActive
    {
        get
        {
            return License is not null && State is LicenseState.Valid or LicenseState.Expiring or LicenseState.Grace;
        }
    }

    /// <summary>Tells whether the license is active and lists the feature.</summary>
    public bool HasFeature(string feature)
    {
        return IsActive && License!.HasFeature(feature);
    }
}

/// <summary>
/// An error in reading, signing or checking a license.
/// </summary>
/// <param name="message">The error message.</param>
public sealed class LicenseException(string message) : Exception(message);
