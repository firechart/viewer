using Flox.Core.Models;

namespace Flox.Shared.Services;

/// <summary>The history of the pages visited on this device, the newest <see cref="RecentVisitService.Capacity"/>; kept on the device only.</summary>
public interface IRecentVisitService
{
    /// <summary>Records the current page; never throws, since a lost visit costs nothing.</summary>
    Task VisitAsync(string displayName, string? category, string? icon, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent visits, at most <paramref name="count"/>, the newest first.</summary>
    Task<IReadOnlyList<UserRecent>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
