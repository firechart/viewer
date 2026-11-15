namespace Flox.Core.Models;

/// <summary>A named input or output of a background task, kept as JSON.</summary>
public class BackgroundTaskParameter
{
    /// <summary>The name of the parameter.</summary>
    public string Name { get; set; } = null!;
    /// <summary>The value as JSON.</summary>
    public string? Value { get; set; }
}
