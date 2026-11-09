using Radzen;

namespace Flox.Shared.Components;

/// <summary>
/// The look of the sheets, shared by the declarative <see cref="ModalSheet"/>/<see cref="BottomSheet"/> and by code that opens a dialog itself.
/// </summary>
public static class SheetOptions
{
    public const string DefaultHeight = "calc(100dvh - 6rem)";
    public const string DefaultModalWidth = "680px";

    public static DialogOptions Modal(string width = DefaultModalWidth, bool showClose = true)
    {
        return new DialogOptions
        {
            Width = width,
            Height = DefaultHeight,
            ShowClose = showClose,
            CloseDialogOnOverlayClick = true,
            CloseDialogOnEsc = true,
            CssClass = "max-sm:inset-0 max-sm:h-dvh max-sm:w-screen max-sm:rounded-none"
        };
    }

    // An ordinary dialog: the title above, the content in the middle (the body scrolls when it does not fit) and the commands below. It
    // is as wide as asked and as high as its content, never larger than the screen, so it suits a phone and a desktop alike
    public static DialogOptions Content(string width = "560px")
    {
        return new DialogOptions
        {
            Width = $"min({width}, calc(100vw - 1rem))",
            ShowClose = true,
            CloseDialogOnOverlayClick = false,
            CloseDialogOnEsc = true
        };
    }

    public static SideDialogOptions Bottom(string height = DefaultHeight, bool showTitle = true, bool showClose = true)
    {
        return new SideDialogOptions
        {
            Position = DialogPosition.Bottom,
            Height = height,
            ShowTitle = showTitle,
            ShowClose = showClose,
            ShowMask = true,
            CloseDialogOnOverlayClick = true,
            Resizable = true
        };
    }
}
