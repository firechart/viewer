using Flox.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

/// <summary>
/// Loads the operations the server grants to the signed-in user.
/// </summary>
public interface IEffectivePermissionsLoader
{
    /// <summary>
    /// Asks the server which operations the signed-in user may perform.
    /// </summary>
    Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Source of the operations the signed-in user may perform.
/// </summary>
public interface IEffectivePermissionsProvider
{
    /// <summary>
    /// Returns the operations granted to the signed-in user.
    /// </summary>
    Task<IReadOnlySet<string>> GetOperationsAsync();

    /// <summary>
    /// Drops the cached operations so the next call loads them again.
    /// </summary>
    void Invalidate();
}

/// <summary>
/// What the server says the signed-in user may do, kept for a short time and dropped when the user changes. A failed load means no permissions (fail closed), retried after a few seconds.
/// </summary>
public sealed class EffectivePermissionsProvider : IEffectivePermissionsProvider, IDisposable
{
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_failureDuration = TimeSpan.FromSeconds(5);

    private readonly IEffectivePermissionsLoader _loader;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<EffectivePermissionsProvider> _logger;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new Lock();
    private Task<IReadOnlySet<string>>? _load;
    private DateTimeOffset _expiresAt;
    private int _generation;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="loader">Loads the operations from the server.</param>
    /// <param name="authenticationService">Tells when the signed-in user changes.</param>
    /// <param name="logger">Logger for load failures.</param>
    /// <param name="time">Clock used for the cache lifetime; the system clock when omitted.</param>
    public EffectivePermissionsProvider(IEffectivePermissionsLoader loader, IAuthenticationService authenticationService, ILogger<EffectivePermissionsProvider> logger, TimeProvider? time = null)
    {
        _loader = loader;
        _authenticationService = authenticationService;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _authenticationService.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetOperationsAsync()
    {
        lock (_gate)
        {
            if (_load == null || _time.GetUtcNow() >= _expiresAt)
            {
                _expiresAt = _time.GetUtcNow() + s_cacheDuration;
                _load = LoadAsync(_generation);
            }

            return _load;
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _load = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _authenticationService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state)
    {
        Invalidate();
    }

    private async Task<IReadOnlySet<string>> LoadAsync(int generation)
    {
        try
        {
            var operations = await _loader.LoadAsync(CancellationToken.None);
            return operations.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot load the effective permissions; every permission is denied for now");
            lock (_gate)
            {
                if (generation == _generation)
                {
                    _expiresAt = _time.GetUtcNow() + s_failureDuration;
                }
            }

            return new HashSet<string>();
        }
    }
}
