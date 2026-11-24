using System.Text.Json.Serialization;

namespace Flox.Licensing;

/// <summary>
/// What a client may know of the license of its server (GET /api/license): the edition and its modes, the modules, the seats and the state; no
/// identifier, owner or sites.
/// </summary>
/// <param name="Edition">The edition the license grants.</param>
/// <param name="Mode">The modes of the license: a trial, a demonstration site.</param>
/// <param name="Modules">The modules the license grants.</param>
/// <param name="State">The state of the license.</param>
/// <param name="ValidUntil">The last day of validity, or null when there is none.</param>
/// <param name="Seats">How many users may work at a time, or null for no limit.</param>
/// <param name="Product">The code of the product the license is for, or null without a license in force.</param>
public sealed record LicenseSummary(LicenseEdition Edition, LicenseMode Mode, IReadOnlyList<string> Modules, LicenseState State, DateOnly? ValidUntil, int? Seats = null, string? Product = null)
{
    /// <summary>The summary of a site without a license in force.</summary>
    public static readonly LicenseSummary Free = new(LicenseEdition.Free, LicenseMode.None, [], LicenseState.None, null, License.FreeSeats);

    /// <summary>Whether the license grants its edition and modules (also while expiring and during the grace period).</summary>
    [JsonIgnore]
    public bool IsActive
    {
        get
        {
            return State is LicenseState.Valid or LicenseState.Expiring or LicenseState.Grace;
        }
    }

    /// <summary>Whether the free edition applies.</summary>
    [JsonIgnore]
    public bool IsFree
    {
        get
        {
            return !IsActive;
        }
    }

    /// <summary>Whether the license is of a demonstration site.</summary>
    [JsonIgnore]
    public bool IsDemo
    {
        get
        {
            return IsActive && Mode.HasFlag(LicenseMode.Demo);
        }
    }

    /// <summary>Whether the license is a trial.</summary>
    [JsonIgnore]
    public bool IsTrial
    {
        get
        {
            return IsActive && Mode.HasFlag(LicenseMode.Trial);
        }
    }

    /// <summary>The edition with its mode as people read it: <c>Enterprise</c>, <c>Enterprise Trial</c>, <c>Enterprise Demo</c>, <c>Free</c>.</summary>
    [JsonIgnore]
    public string Title
    {
        get
        {
            return License.Title(Edition, Mode);
        }
    }

    /// <summary>Makes the summary of a status; a license not in force is the free edition, its state and last day stay, so that an administrator sees why.</summary>
    public static LicenseSummary Of(LicenseStatus status)
    {
        var until = LastDay(status.License);
        return status is { IsActive: true, License: { } license }
            ? new LicenseSummary(license.Edition, license.Mode, license.Modules, status.State, until, license.Seats, license.Product)
            : Free with { State = status.State, ValidUntil = until };
    }

    /// <summary>Tells whether the license is active and grants the module.</summary>
    public bool HasModule(string module)
    {
        return IsActive && Modules.Contains(module, StringComparer.Ordinal);
    }

    private static DateOnly? LastDay(License? license)
    {
        return license?.Ends is { } end ? DateOnly.FromDateTime(end.AddTicks(-1).UtcDateTime) : null;
    }
}
