using FluentValidation;

namespace Flox.Core.Data;

/// <summary>Base class of the validators of entities.</summary>
public abstract class EntityValidator<TEntity> : AbstractValidator<TEntity>
{
    /// <summary>Tells whether the text is an absolute http or https address.</summary>
    /// <param name="link">The text to check.</param>
    protected static bool IsUrl(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var outUri) && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps);
    }
}
