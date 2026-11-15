namespace Flox.Core.Authorization;

/// <summary>
/// Operations a module exposes to the permission matrix, for example "FhirPackage.Index". Register with [FloxService(ServiceType = typeof(IFloxOperationSource), Lifetime = ServiceLifetime.Singleton, Multiple = true)].
/// </summary>
public interface IFloxOperationSource
{
    /// <summary>
    /// The group of the operations on the page of the roles.
    /// </summary>
    string Category
    {
        get
        {
            return "Flox";
        }
    }

    /// <summary>
    /// Returns the names of the operations.
    /// </summary>
    IReadOnlyList<string> GetOperations();
}
