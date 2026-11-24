using BlazorBlueprint.Components;

namespace Flox.Shared.Components;

/// <summary>
/// The kind of a snack message: its colour and icon.
/// </summary>
public enum SnackSeverity
{
    /// <summary>
    /// A neutral message.
    /// </summary>
    Info,

    /// <summary>
    /// Something done.
    /// </summary>
    Success,

    /// <summary>
    /// Something the user should know.
    /// </summary>
    Warning,

    /// <summary>
    /// Something that failed.
    /// </summary>
    Error
}

/// <summary>
/// Short messages at the bottom of the screen (the toasts of Blazor Blueprint), with an optional command such as Undo.
/// </summary>
/// <param name="toasts">The toast service of Blazor Blueprint.</param>
public sealed class FloxSnackbar(ToastService toasts)
{
    /// <summary>
    /// How long a message stays by default, in milliseconds.
    /// </summary>
    public const int DefaultDuration = 4000;

    /// <summary>
    /// Shows a message.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="severity">The kind of the message.</param>
    /// <param name="duration">How long it stays, in milliseconds; 0 keeps it until the user closes it.</param>
    /// <param name="actionText">The caption of the command of the message.</param>
    /// <param name="action">The command of the message.</param>
    /// <returns>The identity of the message, for <see cref="Dismiss"/>.</returns>
    public string Show(string message, SnackSeverity severity = SnackSeverity.Info, int duration = DefaultDuration, string? actionText = null, Action? action = null)
    {
        var toast = new ToastData
        {
            Description = message,
            Variant = severity switch
            {
                SnackSeverity.Success => ToastVariant.Success,
                SnackSeverity.Warning => ToastVariant.Warning,
                SnackSeverity.Error => ToastVariant.Destructive,
                _ => ToastVariant.Default
            },
            ShowIcon = severity != SnackSeverity.Info,
            Duration = duration,
            ActionText = actionText,
            OnAction = action,
            // A long word (a path, a URL) wraps instead of pushing the close button out of the window
            Class = "wrap-anywhere"
        };
        toasts.Show(toast);
        return toast.Id;
    }

    /// <summary>
    /// Closes a message.
    /// </summary>
    public void Dismiss(string id)
    {
        toasts.Dismiss(id);
    }
}
