namespace Flox.Core.Models;

public sealed record EffectivePermissions
(
    IReadOnlyList<string> Operations
);
