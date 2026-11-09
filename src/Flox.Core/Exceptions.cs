using Flox.Core.Data;

namespace Flox.Core;

public class FloxException : Exception
{
    public FloxException(string? message) : base(message) { }
    public FloxException(string? message, Exception? innerException) : base(message, innerException) { }
}

public class UnauthenticatedException : UnauthorizedAccessException
{
    public UnauthenticatedException(string message) : base(message)
    {
    }

    public UnauthenticatedException(IGuidEntity? entity = null) : base("Not authenticated")
    {
        Entity = entity;
    }

    public IGuidEntity? Entity { get; }
}

// Matches HTTP status 403 (Forbidden)
public abstract class ForbidException : UnauthorizedAccessException
{
    protected ForbidException(string message, IGuidEntity? entity = null) : base(message)
    {
        Entity = entity;
    }

    public IGuidEntity? Entity { get; }
}

public class ForbidDocumentException : ForbidException
{
    public ForbidDocumentException(string message) : base(message)
    {
    }

    public ForbidDocumentException(IGuidEntity entity) : base($"Access denied: {entity.Uid}")
    {
    }
}

public class ForbidNotAdminException : ForbidException
{
    public ForbidNotAdminException(IGuidEntity entity) : base($"Access denied (not admin): {entity.Uid}")
    {
    }

    public ForbidNotAdminException() : base($"Access denied (not admin)")
    {
    }
}

public class NotFoundException : FloxException
{
    public NotFoundException(object key) : base($"Entity not found: {key}")
    {
        Key = key;
    }

    public NotFoundException(IGuidEntity entity) : base($"Entity not found: {entity.Uid}")
    {
        Key = entity.Uid;
    }

    public object Key { get; private set; }
}

public class BatchLimitExceededException : FloxException
{
    public BatchLimitExceededException(long count, int limit) : base($"The operation affects {count} items, more than the limit of {limit}. Nothing was changed.")
    {
        Count = count;
        Limit = limit;
    }

    public long Count { get; }

    public int Limit { get; }
}

// The request is right but the present state of the data does not allow it (409); not a defect, not a mistake of the caller
public class ConflictException : FloxException
{
    public ConflictException(string message) : base(message)
    {
    }
}

// The document was changed by somebody else after the caller read it (the revision the caller sends is not the present one)
public class ConcurrentUpdateException : FloxException
{
    public ConcurrentUpdateException(object key) : base($"The data was changed by someone else since it was read: load it again and repeat the change. ({key})")
    {
        Key = key;
    }

    public object Key { get; private set; }
}

public class DuplicateException : FloxException
{
    public DuplicateException(object key) : base($"Entity already exists: {key}")
    {
        Key = key;
    }

    public DuplicateException(IGuidEntity entity) : base($"Entity already exists: {entity.Uid}")
    {
        Key = entity.Uid;
    }

    public object Key { get; private set; }
}
