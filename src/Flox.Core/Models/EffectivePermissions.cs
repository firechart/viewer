namespace Flox.Core.Models;

/// <summary>The operations the server grants to the caller.</summary>
/// <param name="Operations">The names of the granted operations.</param>
public sealed record EffectivePermissions
(
    IReadOnlyList<string> Operations
);
