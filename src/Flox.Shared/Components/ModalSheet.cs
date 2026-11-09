namespace Flox.Shared.Components;

/// <summary>
/// A centered modal dialog that takes the whole screen on phones.
/// </summary>
public class ModalSheet : SheetBase
{
    [Parameter]
    public string MaxWidth { get; set; } = SheetOptions.DefaultModalWidth;

    protected override async Task ShowAsync()
    {
        await Dialogs.OpenAsync<SheetHost>(TitleText, HostParameters, SheetOptions.Modal(MaxWidth, ShowCloseButton));
    }

    protected override void CloseDialog()
    {
        Dialogs.Close();
    }
}
