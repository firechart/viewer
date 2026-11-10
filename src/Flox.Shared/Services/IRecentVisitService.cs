using Flox.Core.Models;

namespace Flox.Shared.Services;

// The history of the pages visited on this device, the newest RecentVisitService.Capacity; kept on the device only
public interface IRecentVisitService
{
    // Records the current page; never throws: a lost visit costs nothing
    Task VisitAsync(string displayName, string? category, string? icon, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRecent>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
