namespace Flox.Licensing;

/// <summary>
/// The license of the server as a client sees it (GET /api/license), loaded once. Only for what the client shows: the server checks by itself.
/// </summary>
public interface IClientLicenseService
{
    /// <summary>
    /// Gets the license summary of the server: <see cref="LicenseSummary.Free"/> when the server has no license in force, or cannot be asked (then asked again next time).
    /// </summary>
    Task<LicenseSummary> GetLicenseAsync(CancellationToken cancellationToken = default);
}
