namespace Flox.Core.Authorization;

/// <summary>
/// Helpers for the "perm:Operation" policy names.
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Prefix of the policy name of an operation. A constant, so it can be used in [Authorize(Policy = PolicyPrefix + "FhirPackage.Index")].
    /// </summary>
    public const string PolicyPrefix = "perm:";

    /// <summary>
    /// Returns the policy name of an operation.
    /// </summary>
    public static string Policy(string operation)
    {
        return PolicyPrefix + operation;
    }
}
