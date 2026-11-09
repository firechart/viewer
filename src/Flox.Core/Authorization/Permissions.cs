namespace Flox.Core.Authorization;

public static class Permissions
{
    // Constant so it can be used in [Authorize(Policy = PolicyPrefix + "FhirPackage.Index")]
    public const string PolicyPrefix = "perm:";

    public static string Policy(string operation)
    {
        return PolicyPrefix + operation;
    }
}
