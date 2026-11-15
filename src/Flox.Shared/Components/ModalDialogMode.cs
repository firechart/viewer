namespace Flox.Shared.Components;

/// <summary>
/// Whether a dialog adds a record or edits one.
/// </summary>
public enum ModalDialogMode
{
    /// <summary>
    /// The dialog creates a record.
    /// </summary>
    Create,
    /// <summary>
    /// The dialog edits an existing record.
    /// </summary>
    Edit
}
