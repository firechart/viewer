namespace Flox.Core.Data;

/// <summary>An error of access to data: a rejected operation or a failed storage.</summary>
public class FloxDataAccessException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public FloxDataAccessException(string? message) : base(message) { }
    /// <summary>Creates the exception with a message and the cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public FloxDataAccessException(string? message, Exception? innerException) : base(message, innerException) { }
}
