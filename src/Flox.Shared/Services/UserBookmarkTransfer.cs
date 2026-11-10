using System.Text.Json;
using System.Text.Json.Serialization;
using Flox.Core.Host;
using Flox.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Shared.Services;

// The bookmarks of this device as a file and back: the way to move them to another device, since they are never sent to a server. An import adds
// what the device has not got (one place is one bookmark) and leaves the rest as it is
[FloxService(typeof(UserBookmarkTransfer), ServiceLifetime.Scoped)]
public sealed class UserBookmarkTransfer(IGuidEntityStorage<UserBookmark> storage)
{
    public const string Format = "flox-bookmarks";
    public const int Version = 1;
    public const long MaxFileSize = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string FileName(DateTimeOffset now)
    {
        return $"bookmarks-{now.UtcDateTime:yyyy-MM-dd}.json";
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var request = new PagedRequest<UserBookmark>
        {
            PageSize = 0,
            ReturnTotal = false
        };
        request.Sorts.SetValue(nameof(UserBookmark.CreatedAt));
        var rows = (await storage.FindAsync(request, cancellationToken)).Items;
        var file = new BookmarkFile(Format, Version, DateTimeOffset.UtcNow, [.. rows.Select(BookmarkFileEntry.Of)]);
        return JsonSerializer.SerializeToUtf8Bytes(file, s_json);
    }

    // A file that is not an export of bookmarks is refused as a whole (InvalidDataException); its entries without a type or an identity are skipped
    public async Task<BookmarkImportResult> ImportAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        BookmarkFile? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync<BookmarkFile>(content, s_json, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a file of bookmarks.", ex);
        }

        if (file is not { Format: Format, Bookmarks: { } entries } || file.Version > Version)
        {
            throw new InvalidDataException("Not a file of bookmarks, or of a newer version.");
        }

        var added = 0;
        var existing = 0;
        var skipped = 0;
        foreach (var entry in entries)
        {
            if (entry.ToBookmark() is not { } bookmark)
            {
                skipped++;
                continue;
            }

            try
            {
                await storage.InsertAsync(bookmark, cancellationToken);
                added++;
            }
            catch (DuplicateException)
            {
                existing++;
            }
        }

        return new BookmarkImportResult(added, existing, skipped);
    }

    private sealed record BookmarkFile(string Format, int Version, DateTimeOffset ExportedAt, List<BookmarkFileEntry>? Bookmarks);

    // The file names the fields: a bookmark of a newer version of the class still reads, and the key is computed again, never taken from the file
    private sealed record BookmarkFileEntry
    (
        string? Type,
        string? EntityId,
        string? DisplayName,
        string? Category,
        string? Url,
        string? SourceKey,
        string? SourceName,
        string? SourceAddress,
        string? Location,
        string? Icon,
        DateTimeOffset? CreatedAt
    )
    {
        public static BookmarkFileEntry Of(UserBookmark row)
        {
            return new BookmarkFileEntry(row.Type, row.EntityId, row.DisplayName, row.Category, row.Url, row.SourceKey, row.SourceName, row.SourceAddress, row.Location, row.Icon, row.CreatedAt);
        }

        public UserBookmark? ToBookmark()
        {
            if (string.IsNullOrWhiteSpace(Type) || string.IsNullOrWhiteSpace(EntityId))
            {
                return null;
            }

            return new UserBookmark
            {
                Uid = UserBookmark.KeyOf(Type, SourceAddress, EntityId),
                Type = Type,
                EntityId = EntityId,
                DisplayName = DisplayName ?? EntityId,
                Category = Category ?? string.Empty,
                Url = Url ?? string.Empty,
                SourceKey = SourceKey,
                SourceName = SourceName,
                SourceAddress = UserBookmark.NormalizeAddress(SourceAddress),
                Location = Location,
                Icon = Icon
            };
        }
    }
}

public sealed record BookmarkImportResult(int Added, int Existing, int Skipped);
