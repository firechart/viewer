namespace Flox.Core.Authorization;

/// <summary>
/// Loader for a client without an application server: nobody signs in, no operation is granted, nothing is asked.
/// </summary>
public sealed class NoEffectivePermissionsLoader : IEffectivePermissionsLoader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
