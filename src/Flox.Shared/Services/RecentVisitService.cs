using System.Security.Cryptography;
using System.Text;
using Flox.Core.Models;
using Microsoft.Extensions.Logging;

namespace Flox.Shared.Services;

[FloxService(typeof(IRecentVisitService))]
public sealed class RecentVisitService(IAuthenticationService authentication, NavigationManager navigation, IServiceProvider serviceProvider, ILogger<RecentVisitService> logger) : IRecentVisitService
{
    public const string PAGE_TYPE = "Page";

    private static readonly TimeSpan s_keepFor = TimeSpan.FromDays(30);

    private bool _pruned;

    // The path without the query: a list with other filters is the same page
    public static string PathOf(string url)
    {
        var end = url.IndexOfAny(['?', '#']);
        return end >= 0 ? url[..end] : url;
    }

    // One row for a user and a page, whatever device or tab visits it
    private static Guid KeyOf(string userId, string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{PAGE_TYPE}|{path}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public async Task VisitAsync(string displayName, string? category, string? icon, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await authentication.GetCurrentUserAsync();
            if (!user.IsAuthenticated() || string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            var url = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            var path = PathOf(url);
            var key = KeyOf(user.GetUserId(), path);

            // A data source of every call: two visits in a row must not share one
            var recent = serviceProvider.GetRequiredService<IGuidDataSource<UserRecent>>();

            // A list with a filter, not a read by key: a page not visited yet is an empty answer, not an error of the client
            recent.SetPageSize(1)
                  .SetCount(false)
                  .Filters.SetValue(nameof(UserRecent.Uid), key);
            await recent.LoadAsync(cancellationToken);
            var row = recent.Items.FirstOrDefault();
            if (row is null)
            {
                row = new UserRecent { Uid = key, Type = PAGE_TYPE, EntityId = path };
                Fill(row);
                await recent.InsertAsync(row, refresh: false, cancellationToken: cancellationToken);
            }
            else
            {
                Fill(row);
                await recent.UpdateAsync(key, row, cancellationToken);
            }

            // After the visit: a slow clean-up must not cost it
            await PruneOnceAsync(recent, cancellationToken);

            void Fill(UserRecent value)
            {
                value.Url = url;
                value.DisplayName = displayName;
                value.Category = category ?? string.Empty;
                value.Icon = icon;
                value.VisitedAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "The visit of {Page} was not recorded", displayName);
        }
    }

    public async Task<IReadOnlyList<UserRecent>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var user = await authentication.GetCurrentUserAsync();
        if (!user.IsAuthenticated())
        {
            return [];
        }

        var list = serviceProvider.GetRequiredService<IGuidDataSource<UserRecent>>();
        list.SetPageSize(count)
            .SetCount(false)
            .Sorts.SetValue(nameof(UserRecent.VisitedAt), ascending: false);
        await list.LoadAsync(cancellationToken);
        return list.Items;
    }

    // Once per session, not per visit
    private async Task PruneOnceAsync(IGuidDataSource<UserRecent> recent, CancellationToken cancellationToken)
    {
        if (_pruned)
        {
            return;
        }

        _pruned = true;
        var before = DateTimeOffset.UtcNow - s_keepFor;
        try
        {
            await recent.DeleteManyAsync(row => row.VisitedAt < before, refresh: false, cancellationToken: cancellationToken);
        }
        catch (BatchLimitExceededException)
        {
            // too many old rows at once: the next sessions take their turn
        }
    }
}
