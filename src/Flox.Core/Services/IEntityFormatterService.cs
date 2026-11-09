using Flox.Core.Data;
using Flox.Core.Models;

namespace Flox.Core.Services;

public interface IEntityFormatterService
{
    EntityDisplayInfo? Format<TKey, TEntity>(TEntity value) where TKey : notnull;
}
