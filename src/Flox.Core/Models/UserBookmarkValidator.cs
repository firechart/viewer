using Flox.Core.Data;
using Flox.Resources;
using FluentValidation;

namespace Flox.Core.Models;

// A bookmark keeps a place (/Patient/123), not a search: the query string of a search may carry what a patient is called or when born,
// and the bookmarks are replicated to the server
public class UserBookmarkValidator : EntityValidator<UserBookmark>
{
    public UserBookmarkValidator()
    {
        RuleFor(row => row.Url)
            .Must(url => !HasQueryString(url))
            .WithMessage(Strings.Message_BookmarkQueryString);
    }

    public static bool HasQueryString(string? url)
    {
        return url is not null && url.Contains('?', StringComparison.Ordinal);
    }
}
