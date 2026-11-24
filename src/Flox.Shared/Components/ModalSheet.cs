namespace Flox.Shared.Components;

/// <summary>
/// A centered modal dialog that takes the whole screen on phones.
/// </summary>
public class ModalSheet : SheetBase
{
    /// <summary>
    /// Maximum width of the dialog as a CSS size.
    /// </summary>
    [Parameter]
    public string MaxWidth { get; set; } = SheetOptions.DefaultModalWidth;

    /// <inheritdoc />
    protected override async Task ShowAsync()
    {
        Shown = Dialogs.OpenAsync<SheetHost>(TitleText, HostParameters, SheetOptions.Modal(MaxWidth, ShowCloseButton));
        await Shown;
    }

    /// <inheritdoc />
    protected override void CloseDialog()
    {
        Dialogs.Close();
    }
}
