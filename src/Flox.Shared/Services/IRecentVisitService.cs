using Flox.Core.Models;

namespace Flox.Shared.Services;

// The history of the pages a signed-in user has visited; a guest has none
public interface IRecentVisitService
{
    // Records the current page; never throws: a lost visit costs nothing
    Task VisitAsync(string displayName, string? category, string? icon, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRecent>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
