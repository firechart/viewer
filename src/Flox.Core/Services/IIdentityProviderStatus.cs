namespace Flox.Core.Services;

/// <summary>
/// Whether the OpenID provider answered the last time the session needed it (renewing an expired token).
/// The session is kept while it does not. Should be scoped; only the clients that own the sign-in flow register it
/// </summary>
public interface IIdentityProviderStatus
{
    /// <summary>Whether the identity provider did not answer the last time it was needed.</summary>
    bool IsUnavailable { get; }

    /// <summary>Raised when <see cref="IsUnavailable"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Tries to reach the identity provider again.</summary>
    Task RetryAsync();
}
