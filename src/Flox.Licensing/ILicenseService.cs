namespace Flox.Licensing;

/// <summary>
/// The license of the running application, read once at the start.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Gets the license in force (valid, expiring or in its grace period), already checked against the keys of the vendor and the site;
    /// <see cref="License.Free"/> without a license, or when it is invalid, not yet valid or expired. Never null.
    /// </summary>
    License GetLicense();

    /// <summary>
    /// Gets the state at this moment, with the license and, when it is invalid, the reason.
    /// </summary>
    LicenseStatus GetStatus();
}
