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
        Shown = Dialogs.OpenSideAsync<SheetHost>(TitleText, HostParameters, SheetOptions.Bottom(Height, !string.IsNullOrEmpty(TitleText), ShowCloseButton));
        await Shown;
    }

    /// <inheritdoc />
    protected override void CloseDialog()
    {
        Dialogs.Close();
    }
}
