using System.Text.Json.Serialization;

namespace Flox.Licensing;

/// <summary>
/// What a client may know of the license of its server (GET /api/license): the edition, the features and the state, no customer, number or limits.
/// </summary>
/// <param name="Edition">The edition the license grants.</param>
/// <param name="Features">The features the license lists.</param>
/// <param name="State">The state of the license.</param>
/// <param name="ValidUntil">The last day of validity, or null when there is none.</param>
public sealed record LicenseSummary(string Edition, IReadOnlyList<string> Features, LicenseState State, DateOnly? ValidUntil)
{
    /// <summary>The summary of a site without a license in force.</summary>
    public static readonly LicenseSummary Free = new(License.FreeEdition, [], LicenseState.None, null);

    /// <summary>Whether the license grants its edition and features (also while expiring and during the grace period).</summary>
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

    /// <summary>Makes the summary of a status; a license not in force is the free edition, its state and last day stay, so that an administrator sees why.</summary>
    public static LicenseSummary Of(LicenseStatus status)
    {
        return status is { IsActive: true, License: { } license }
            ? new LicenseSummary(license.Edition, license.Features, status.State, license.ValidUntil)
            : Free with { State = status.State, ValidUntil = status.License?.ValidUntil };
    }

    /// <summary>Tells whether the license is active and lists the feature.</summary>
    public bool HasFeature(string feature)
    {
        return IsActive && Features.Contains(feature, StringComparer.Ordinal);
    }
}
