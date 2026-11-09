namespace Flox.Core.Data;

public class FloxDataAccessException : Exception
{
    public FloxDataAccessException(string? message) : base(message) { }
    public FloxDataAccessException(string? message, Exception? innerException) : base(message, innerException) { }
}
