namespace Flox.Core.Authorization;

/// <summary>
/// Names of the operations the row-level access rules distinguish.
/// </summary>
public static class AccessOperations
{
    /// <summary>
    /// Reading a row.
    /// </summary>
    public const string Read = nameof(Read);
    /// <summary>
    /// Changing a row.
    /// </summary>
    public const string Write = nameof(Write);
    /// <summary>
    /// Deleting a row.
    /// </summary>
    public const string Delete = nameof(Delete);
}
