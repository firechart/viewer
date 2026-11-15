namespace Flox.Shared.Components;

/// <summary>
/// A sheet: a panel with a title that opens above the page and holds arbitrary content.
/// </summary>
public interface IFloxSheet
{
    /// <summary>
    /// Opens the sheet.
    /// </summary>
    Task OpenAsync();
    /// <summary>
    /// Closes the sheet.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// The title of the sheet.
    /// </summary>
    string TitleText { get; set; }

    /// <summary>
    /// Whether the sheet is open.
    /// </summary>
    bool IsOpen { get; set; }

    /// <summary>
    /// Whether the sheet shows a close button.
    /// </summary>
    bool ShowCloseButton { get; set; }

    /// <summary>
    /// The content of the sheet.
    /// </summary>
    RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Commands shown in the header of the sheet.
    /// </summary>
    RenderFragment? HeaderCommands { get; set; }
}
