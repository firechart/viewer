namespace Flox.Shared.Components;

/// <summary>
/// The kind of an <c>Alert</c>: its colour and icon.
/// </summary>
public enum AlertKind
{
    /// <summary>
    /// Something to know.
    /// </summary>
    Info,

    /// <summary>
    /// Something done.
    /// </summary>
    Success,

    /// <summary>
    /// Something to take care of.
    /// </summary>
    Warning,

    /// <summary>
    /// Something that failed.
    /// </summary>
    Danger
}
