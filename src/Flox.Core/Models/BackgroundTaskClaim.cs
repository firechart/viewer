namespace Flox.Core.Models;

/// <summary>A claim of the user a background task runs for.</summary>
public class BackgroundTaskClaim
{
    /// <summary>The claim type.</summary>
    public string Type { get; set; } = null!;
    /// <summary>The claim value.</summary>
    public string Value { get; set; } = null!;
}
