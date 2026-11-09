namespace Flox.Core.Authorization;

// Operations a module exposes to the permission matrix, e.g. "FhirPackage.Index"; register with
// [FloxService(ServiceType = typeof(IFloxOperationSource), Lifetime = ServiceLifetime.Singleton, Multiple = true)]
public interface IFloxOperationSource
{
    // The group of the operations on the page of the roles
    string Category
    {
        get
        {
            return "Flox";
        }
    }

    IReadOnlyList<string> GetOperations();
}
