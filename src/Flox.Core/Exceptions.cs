using Flox.Core.Data;

namespace Flox.Core;

/// <summary>
/// Base of the exceptions of the platform.
/// </summary>
public class FloxException : Exception
{
    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public FloxException(string? message) : base(message) { }
    /// <summary>
    /// Creates the exception with a message and the exception that caused it.
    /// </summary>
    public FloxException(string? message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// The caller is not signed in; matches HTTP status 401.
/// </summary>
public class UnauthenticatedException : UnauthorizedAccessException
{
    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public UnauthenticatedException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception for an entity the caller tried to reach.
    /// </summary>
    public UnauthenticatedException(IGuidEntity? entity = null) : base("Not authenticated")
    {
        Entity = entity;
    }

    /// <summary>
    /// The entity the caller tried to reach, if known.
    /// </summary>
    public IGuidEntity? Entity { get; }
}

/// <summary>
/// The caller is signed in but not allowed; matches HTTP status 403 (Forbidden).
/// </summary>
public abstract class ForbidException : UnauthorizedAccessException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What was denied.</param>
    /// <param name="entity">The entity the access was denied to, if any.</param>
    protected ForbidException(string message, IGuidEntity? entity = null) : base(message)
    {
        Entity = entity;
    }

    /// <summary>
    /// The entity the access was denied to, if known.
    /// </summary>
    public IGuidEntity? Entity { get; }
}

/// <summary>
/// Access to a document is denied.
/// </summary>
public class ForbidDocumentException : ForbidException
{
    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public ForbidDocumentException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception for the entity.
    /// </summary>
    public ForbidDocumentException(IGuidEntity entity) : base($"Access denied: {entity.Uid}")
    {
    }
}

/// <summary>
/// The operation is for administrators only.
/// </summary>
public class ForbidNotAdminException : ForbidException
{
    /// <summary>
    /// Creates the exception for the entity.
    /// </summary>
    public ForbidNotAdminException(IGuidEntity entity) : base($"Access denied (not admin): {entity.Uid}")
    {
    }

    /// <summary>
    /// Creates the exception.
    /// </summary>
    public ForbidNotAdminException() : base($"Access denied (not admin)")
    {
    }
}

/// <summary>
/// The entity does not exist.
/// </summary>
public class NotFoundException : FloxException
{
    /// <summary>
    /// Creates the exception for the key that was not found.
    /// </summary>
    public NotFoundException(object key) : base($"Entity not found: {key}")
    {
        Key = key;
    }

    /// <summary>
    /// Creates the exception for the entity that was not found.
    /// </summary>
    public NotFoundException(IGuidEntity entity) : base($"Entity not found: {entity.Uid}")
    {
        Key = entity.Uid;
    }

    /// <summary>
    /// The key that was not found.
    /// </summary>
    public object Key { get; private set; }
}

/// <summary>
/// A batch operation would touch more items than allowed.
/// </summary>
public class BatchLimitExceededException : FloxException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="count">How many items the operation would affect.</param>
    /// <param name="limit">The largest number allowed.</param>
    public BatchLimitExceededException(long count, int limit) : base($"The operation affects {count} items, more than the limit of {limit}. Nothing was changed.")
    {
        Count = count;
        Limit = limit;
    }

    /// <summary>
    /// How many items the operation would affect.
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// The largest number of items allowed.
    /// </summary>
    public int Limit { get; }
}

/// <summary>
/// The request is right but the present state of the data does not allow it (409); not a defect, not a mistake of the caller.
/// </summary>
public class ConflictException : FloxException
{
    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>
/// The document was changed by somebody else after the caller read it (the revision the caller sends is not the present one).
/// </summary>
public class ConcurrentUpdateException : FloxException
{
    /// <summary>
    /// Creates the exception for the key of the document.
    /// </summary>
    public ConcurrentUpdateException(object key) : base($"The data was changed by someone else since it was read: load it again and repeat the change. ({key})")
    {
        Key = key;
    }

    /// <summary>
    /// The key of the document that was changed.
    /// </summary>
    public object Key { get; private set; }
}

/// <summary>
/// The entity already exists.
/// </summary>
public class DuplicateException : FloxException
{
    /// <summary>
    /// Creates the exception for the key that is taken.
    /// </summary>
    public DuplicateException(object key) : base($"Entity already exists: {key}")
    {
        Key = key;
    }

    /// <summary>
    /// Creates the exception for the entity that exists.
    /// </summary>
    public DuplicateException(IGuidEntity entity) : base($"Entity already exists: {entity.Uid}")
    {
        Key = entity.Uid;
    }

    /// <summary>
    /// The key that is taken.
    /// </summary>
    public object Key { get; private set; }
}
