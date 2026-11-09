using FluentValidation;

namespace Flox.Core.Data;

public abstract class EntityValidator<TEntity> : AbstractValidator<TEntity>
{
    protected static bool IsUrl(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var outUri) && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps);
    }
}
