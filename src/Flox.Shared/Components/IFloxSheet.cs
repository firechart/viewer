namespace Flox.Shared.Components;

public interface IFloxSheet
{
    Task OpenAsync();
    Task CloseAsync();

    string TitleText { get; set; }

    bool IsOpen { get; set; }

    bool ShowCloseButton { get; set; }

    RenderFragment? ChildContent { get; set; }

    RenderFragment? HeaderCommands { get; set; }
}
