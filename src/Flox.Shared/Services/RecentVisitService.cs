using System.Security.Cryptography;
using System.Text;
using Flox.Core.Models;
using Microsoft.Extensions.Logging;

namespace Flox.Shared.Services;

[FloxService(typeof(IRecentVisitService))]
public sealed class RecentVisitService(NavigationManager navigation, IServiceProvider serviceProvider, ILogger<RecentVisitService> logger) : IRecentVisitService
{
    public const string PAGE_TYPE = "Page";

    // A queue: the visits beyond it, the oldest, are deleted by the next visit
    public const int Capacity = 200;

    // The path without the query: a list with other filters is the same page
    public static string PathOf(string url)
    {
        var end = url.IndexOfAny(['?', '#']);
        return end >= 0 ? url[..end] : url;
    }

    // One row for a page, whatever tab visits it
    private static Guid KeyOf(string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{PAGE_TYPE}|{path}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public async Task VisitAsync(string displayName, string? category, string? icon, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            var url = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            var path = PathOf(url);
            var key = KeyOf(path);

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
                await TrimAsync(cancellationToken);
            }
            else
            {
                Fill(row);
                await recent.UpdateAsync(key, row, cancellationToken);
            }

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
        var list = serviceProvider.GetRequiredService<IGuidDataSource<UserRecent>>();
        list.SetPageSize(count)
            .SetCount(false)
            .Sorts.SetValue(nameof(UserRecent.VisitedAt), ascending: false);
        await list.LoadAsync(cancellationToken);
        return list.Items;
    }

    // Only a new row can overflow the queue: an update moves a row to its head
    private async Task TrimAsync(CancellationToken cancellationToken)
    {
        var storage = serviceProvider.GetRequiredService<IGuidEntityStorage<UserRecent>>();
        if (await storage.CountAsync(cancellationToken: cancellationToken) <= Capacity)
        {
            return;
        }

        var request = new PagedRequest<UserRecent>
        {
            Skip = Capacity,
            PageSize = 0,
            ReturnTotal = false
        };
        request.Sorts.SetValue(nameof(UserRecent.VisitedAt), ascending: false);
        var oldest = (await storage.FindAsync(request, cancellationToken)).Items.Select(row => row.Uid).ToHashSet();
        await storage.DeleteManyAsync(row => oldest.Contains(row.Uid), cancellationToken: cancellationToken);
    }
}
