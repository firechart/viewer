namespace Flox.Shared.Components;

/// <summary>
/// A side dialog docked to the bottom edge; the user can resize it by dragging its edge.
/// </summary>
public class BottomSheet : SheetBase
{
    /// <summary>
    /// Height of the sheet as a CSS size.
    /// </summary>
    [Parameter]
    public string Height { get; set; } = SheetOptions.DefaultHeight;

    /// <inheritdoc />
    protected override async Task ShowAsync()
    {
        await Dialogs.OpenSideAsync<SheetHost>(TitleText, HostParameters, SheetOptions.Bottom(Height, !string.IsNullOrEmpty(TitleText), ShowCloseButton));
    }

    /// <inheritdoc />
    protected override void CloseDialog()
    {
        Dialogs.CloseSide();
    }
}
