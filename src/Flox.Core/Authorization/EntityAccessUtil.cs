using System.Linq.Expressions;
using System.Security.Claims;
using Flox.Core.Data;

namespace Flox.Core.Authorization;

/// <summary>
/// Row-level rules of Entity.ReadAccess and Entity.WriteAccess. The owner reads their own rows whatever the level is; the other levels are Public (anyone, reading only), Authenticated (any signed-in user) and Role (members of ReadRole or WriteRole). Whether the caller may use the table at all is the other layer (Orchard's permissions, EntityPermissions): both must agree.
/// </summary>
public static class EntityAccessUtil
{
    /// <summary>
    /// Restricts a query to the rows the caller may access for the operation; an administrator gets everything.
    /// </summary>
    public static IQueryable<TEntity> Authorized<TEntity>(this IQueryable<TEntity> queryable, ClaimsPrincipal user, string operation = AccessOperations.Read) where TEntity : IEntity
    {
        return queryable.Where(CreateExpression<TEntity>(user, operation));
    }

    /// <summary>
    /// Restricts a query to the rows the caller may read by the rules of the rows, an administrator's included: the private rows of others are reached by key only.
    /// </summary>
    public static IQueryable<TEntity> Listed<TEntity>(this IQueryable<TEntity> queryable, ClaimsPrincipal user) where TEntity : IEntity
    {
        return queryable.Where(CreateExpression<TEntity>(user, AccessOperations.Read, administratorBypass: false));
    }

    /// <summary>
    /// Builds the access rule as a condition on an entity, for the database (Marten translates it to SQL) or for a list, with the caller's identity substituted in as constants.
    /// </summary>
    public static Expression<Func<TEntity, bool>> CreateExpression<TEntity>(ClaimsPrincipal user, string operation, bool administratorBypass = true) where TEntity : IEntity
    {
        ArgumentNullException.ThrowIfNull(user);

        if (administratorBypass && user.IsAdmin())
        {
            return _ => true;
        }

        var rule = AccessRules<TEntity>.Of(operation);
        var body = new ConstantSubstitution(rule.Parameters[1], Expression.Constant(user.GetUserId()), rule.Parameters[2], Expression.Constant(user.IsAuthenticated()), rule.Parameters[3], Expression.Constant(RolesOf(user))).Visit(rule.Body);
        return Expression.Lambda<Func<TEntity, bool>>(body, rule.Parameters[0]);
    }

    /// <summary>
    /// Applies the access rule to an entity that is already loaded; the rule is compiled once per entity type and operation.
    /// </summary>
    public static bool CanAccess<TEntity>(ClaimsPrincipal user, TEntity entity, string operation) where TEntity : IEntity
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsAdmin())
        {
            return true;
        }

        return AccessRules<TEntity>.CompiledOf(operation)(entity, user.GetUserId(), user.IsAuthenticated(), RolesOf(user));
    }

    private static string[] RolesOf(ClaimsPrincipal user)
    {
        return user.IsAuthenticated() ? [.. user.GetRoles()] : [];
    }

    private static class AccessRules<TEntity> where TEntity : IEntity
    {
        // Plain comparisons only (no method calls but Contains of the roles), so that the same rule runs in memory and is translated to SQL by Marten.
        // An anonymous caller owns nothing: its user id is the empty one, which is also the owner of a row nobody has claimed
        private static readonly Expression<Func<TEntity, string, bool, string[], bool>> s_read = (row, userId, authenticated, roles) =>
            row.ReadAccess == Access.Public ||
            (authenticated && (row.OwnerId == userId || row.ReadAccess == Access.Authenticated || (row.ReadAccess == Access.Role && roles.Contains(row.ReadRole!))));

        // Write and Delete need the right to read the document as well
        private static readonly Expression<Func<TEntity, string, bool, string[], bool>> s_write = (row, userId, authenticated, roles) =>
            authenticated &&
            (row.ReadAccess == Access.Public || row.OwnerId == userId || row.ReadAccess == Access.Authenticated || (row.ReadAccess == Access.Role && roles.Contains(row.ReadRole!))) &&
            ((row.WriteAccess == Access.Owner && row.OwnerId == userId) || row.WriteAccess == Access.Authenticated || (row.WriteAccess == Access.Role && roles.Contains(row.WriteRole!)));

        private static readonly Func<TEntity, string, bool, string[], bool> s_readCompiled = s_read.Compile();
        private static readonly Func<TEntity, string, bool, string[], bool> s_writeCompiled = s_write.Compile();

        public static Expression<Func<TEntity, string, bool, string[], bool>> Of(string operation)
        {
            return operation == AccessOperations.Read ? s_read : s_write;
        }

        public static Func<TEntity, string, bool, string[], bool> CompiledOf(string operation)
        {
            return operation == AccessOperations.Read ? s_readCompiled : s_writeCompiled;
        }
    }

    private sealed class ConstantSubstitution
    (
        ParameterExpression userIdParameter,
        ConstantExpression userId,
        ParameterExpression authenticatedParameter,
        ConstantExpression authenticated,
        ParameterExpression rolesParameter,
        ConstantExpression roles
    ) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == userIdParameter)
            {
                return userId;
            }

            if (node == authenticatedParameter)
            {
                return authenticated;
            }

            return node == rolesParameter ? roles : base.VisitParameter(node);
        }
    }
}
