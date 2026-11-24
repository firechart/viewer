using System.Text.Json.Serialization;

namespace Flox.Licensing;

/// <summary>
/// The edition of a server: what its license is. A trial and a demonstration site are modes of an edition (<see cref="LicenseMode"/>), not editions.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LicenseEdition>))]
public enum LicenseEdition
{
    /// <summary>No license in force: the public image, <see cref="License.FreeSeats"/> seats, no modules.</summary>
    Free,

    /// <summary>A license of an organization: its seats and its modules.</summary>
    Enterprise,
}

/// <summary>
/// The modes of a license besides its edition.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<LicenseMode>))]
public enum LicenseMode
{
    /// <summary>An ordinary license.</summary>
    None = 0,

    /// <summary>A trial: for a while, for its sites or, naming none, for any.</summary>
    Trial = 1,

    /// <summary>A demonstration site: every user who registers gets a seat while there are seats; for its sites or, naming none, for any.</summary>
    Demo = 2,
}

/// <summary>
/// What a license grants, independent of the service that issued it: the edition and its modes, the modules, the seats, the sites and the dates.
/// </summary>
public sealed record License
{
    /// <summary>How many users a server without a license in force has (the public image): the free edition.</summary>
    public const int FreeSeats = 2;

    /// <summary>What <see cref="ILicenseService.GetLicense"/> gives without a license in force: no modules, <see cref="FreeSeats"/> seats. Its real
    /// state is in <see cref="LicenseStatus"/>.</summary>
    public static readonly License Free = new()
    {
        Id = "free",
        Product = "",
        Edition = LicenseEdition.Free,
        Seats = FreeSeats,
        Issued = DateTimeOffset.UnixEpoch,
    };

    /// <summary>The identifier of the license at its issuer.</summary>
    public required string Id { get; init; }

    /// <summary>The display name of the license, or null.</summary>
    public string? Name { get; init; }

    /// <summary>The code of the product the license is for.</summary>
    public required string Product { get; init; }

    /// <summary>The name of the policy (the template) the license was issued under, or null.</summary>
    public string? Policy { get; init; }

    /// <summary>The edition the license grants.</summary>
    public required LicenseEdition Edition { get; init; }

    /// <summary>The modes of the license: a trial, a demonstration site.</summary>
    public LicenseMode Mode { get; init; }

    /// <summary>The codes of the modules the license grants beside its edition; what they mean is the product's business.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>How many users may work at a time, or null for no limit.</summary>
    public int? Seats { get; init; }

    /// <summary>The public addresses of the sites the license is for (Flox:Oidc:Authority).</summary>
    public IReadOnlyList<string> Audience { get; init; } = [];

    /// <summary>The moment the license ends, or null for a license without an end.</summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>The moment the license document was issued.</summary>
    public required DateTimeOffset Issued { get; init; }

    /// <summary>The moment the license document itself stops being valid (a new one is issued with the renewal), or null.</summary>
    public DateTimeOffset? DocumentExpires { get; init; }

    /// <summary>The moment the license stops granting anything: the earlier of <see cref="Expires"/> and <see cref="DocumentExpires"/>, or null.</summary>
    [JsonIgnore]
    public DateTimeOffset? Ends
    {
        get
        {
            return (Expires, DocumentExpires) switch
            {
                ({ } license, { } document) => license < document ? license : document,
                ({ } license, null) => license,
                (null, var document) => document,
            };
        }
    }

    /// <summary>Whether the license is of a demonstration site.</summary>
    [JsonIgnore]
    public bool IsDemo
    {
        get
        {
            return Mode.HasFlag(LicenseMode.Demo);
        }
    }

    /// <summary>Whether the license is a trial.</summary>
    [JsonIgnore]
    public bool IsTrial
    {
        get
        {
            return Mode.HasFlag(LicenseMode.Trial);
        }
    }

    /// <summary>Whether the license may name no site and then holds for any: a demonstration site or a trial. Every other license names its sites.</summary>
    [JsonIgnore]
    public bool MayNameNoSite
    {
        get
        {
            return Mode != LicenseMode.None;
        }
    }

    /// <summary>Whether this is the free edition, not a license.</summary>
    [JsonIgnore]
    public bool IsFree
    {
        get
        {
            return Edition == LicenseEdition.Free;
        }
    }

    /// <summary>Tells whether the license grants the module.</summary>
    public bool HasModule(string module)
    {
        return Modules.Contains(module, StringComparer.Ordinal);
    }

    /// <summary>Tells whether the license is for the site, by its public address (a trailing slash and the case do not matter); a demonstration
    /// or a trial license that names no site is for any.</summary>
    public bool IsFor(string audience)
    {
        if (Audience.Count == 0 && MayNameNoSite)
        {
            return true;
        }

        var site = audience.TrimEnd('/');
        return Audience.Any(address => string.Equals(address.TrimEnd('/'), site, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The edition with its mode as people read it: <c>Enterprise</c>, <c>Enterprise Trial</c>, <c>Enterprise Demo</c>, <c>Free</c>.</summary>
    public static string Title(LicenseEdition edition, LicenseMode mode)
    {
        return mode.HasFlag(LicenseMode.Demo) ? edition + " Demo" : mode.HasFlag(LicenseMode.Trial) ? edition + " Trial" : edition.ToString();
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

    /// <summary>Less than <see cref="LicenseCheck.WarningPeriod"/> before the end.</summary>
    Expiring,

    /// <summary>Ended less than <see cref="LicenseCheck.GracePeriod"/> ago: everything works as before.</summary>
    Grace,

    /// <summary>The grace period is over.</summary>
    Expired,

    /// <summary>The document is issued later than now (a clock that is behind).</summary>
    NotYetValid,

    /// <summary>The signature, the issuer, the product or the site do not match, the license is suspended, or a field is missing.</summary>
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

    /// <summary>Whether the license grants its edition and modules (also while expiring and during the grace period).</summary>
    public bool IsActive
    {
        get
        {
            return License is not null && State is LicenseState.Valid or LicenseState.Expiring or LicenseState.Grace;
        }
    }

    /// <summary>Makes the status of a license that cannot be accepted.</summary>
    public static LicenseStatus Invalid(string error, License? license = null)
    {
        return new LicenseStatus(LicenseState.Invalid, license, error);
    }

    /// <summary>Tells whether the license is active and grants the module.</summary>
    public bool HasModule(string module)
    {
        return IsActive && License!.HasModule(module);
    }
}

/// <summary>
/// An error in reading or checking a license.
/// </summary>
/// <param name="message">The error message.</param>
public sealed class LicenseException(string message) : Exception(message);
