namespace Flox.Shared.Components;

/// <summary>
/// The look of the sheets, shared by the declarative <see cref="ModalSheet"/>/<see cref="BottomSheet"/> and by code that opens a dialog itself.
/// </summary>
public static class SheetOptions
{
    /// <summary>
    /// The default height of a modal sheet and of a bottom sheet.
    /// </summary>
    public const string DefaultHeight = "calc(100dvh - 6rem)";

    /// <summary>
    /// The default width of a modal sheet.
    /// </summary>
    public const string DefaultModalWidth = "680px";

    /// <summary>
    /// Options of a centered modal dialog that takes the whole screen on phones.
    /// </summary>
    public static FloxDialogOptions Modal(string width = DefaultModalWidth, bool showClose = true)
    {
        return new FloxDialogOptions
        {
            Width = width,
            Height = DefaultHeight,
            ShowClose = showClose,
            CloseOnOverlayClick = true,
            CloseOnEscape = true,
            FullScreenOnPhone = true
        };
    }

    /// <summary>
    /// Options of an ordinary dialog: the title above, the content in the middle (the body scrolls when it does not fit) and the commands below. It is as wide as asked and as high as its content, never larger than the screen.
    /// </summary>
    public static FloxDialogOptions Content(string width = "560px")
    {
        return new FloxDialogOptions
        {
            Width = width,
            ShowClose = true,
            CloseOnOverlayClick = false,
            CloseOnEscape = true
        };
    }

    /// <summary>
    /// Options of a sheet docked to the bottom edge.
    /// </summary>
    public static FloxDialogOptions Bottom(string height = DefaultHeight, bool showTitle = true, bool showClose = true)
    {
        return new FloxDialogOptions
        {
            Placement = FloxDialogPlacement.Bottom,
            Height = height,
            ShowTitle = showTitle,
            ShowClose = showClose,
            CloseOnOverlayClick = true,
            CloseOnEscape = true
        };
    }
}
