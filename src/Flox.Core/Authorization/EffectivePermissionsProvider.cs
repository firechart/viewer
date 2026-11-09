using Flox.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

public interface IEffectivePermissionsLoader
{
    Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken);
}

public interface IEffectivePermissionsProvider
{
    Task<IReadOnlySet<string>> GetOperationsAsync();

    void Invalidate();
}

// What the server says the signed-in user may do, kept for a short time and dropped when the user
// changes. A failed load means no permissions (fail closed), retried after a few seconds
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

    public EffectivePermissionsProvider(IEffectivePermissionsLoader loader, IAuthenticationService authenticationService, ILogger<EffectivePermissionsProvider> logger, TimeProvider? time = null)
    {
        _loader = loader;
        _authenticationService = authenticationService;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _authenticationService.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

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

    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _load = null;
        }
    }

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
