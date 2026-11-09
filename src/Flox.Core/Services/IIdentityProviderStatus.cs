namespace Flox.Core.Services;

/// <summary>
/// Whether the OpenID provider answered the last time the session needed it (renewing an expired token).
/// The session is kept while it does not. Should be scoped; only the clients that own the sign-in flow register it
/// </summary>
public interface IIdentityProviderStatus
{
    bool IsUnavailable { get; }

    event EventHandler? Changed;

    Task RetryAsync();
}
