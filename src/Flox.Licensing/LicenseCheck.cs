namespace Flox.Licensing;

/// <summary>
/// The rules every license follows once its document is read and its signature is checked: the site, the dates, the warning and the grace periods.
/// </summary>
public static class LicenseCheck
{
    /// <summary>How long before the end a license counts as expiring.</summary>
    public static readonly TimeSpan WarningPeriod = TimeSpan.FromDays(30);

    /// <summary>How long after the end a license keeps working.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    /// <summary>How far ahead of the clock a document may be issued (clocks differ a little).</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Checks a license, whose document is read and signature checked, against the site at a moment.</summary>
    /// <param name="license">The license.</param>
    /// <param name="audience">The public address of the site (Flox:Oidc:Authority).</param>
    /// <param name="now">The moment to judge the license at.</param>
    public static LicenseStatus Check(License license, string audience, DateTimeOffset now)
    {
        // A demonstration site and a trial may name no site; every other license is bound to its sites
        if (license.Audience.Count == 0 && !license.MayNameNoSite)
        {
            return LicenseStatus.Invalid("The license names no site", license);
        }
        if (!license.IsFor(audience))
        {
            return LicenseStatus.Invalid("The license is for another site: " + string.Join(", ", license.Audience), license);
        }
        return new LicenseStatus(StateAt(license, now), license, null);
    }

    /// <summary>Gets the state of an accepted license at a moment.</summary>
    public static LicenseState StateAt(License license, DateTimeOffset now)
    {
        if (now + ClockSkew < license.Issued)
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
}
