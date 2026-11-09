using Radzen;

namespace Flox.Shared.Components;

public static class DialogExtensions
{
    /// <returns><c>true</c> for yes/OK, <c>false</c> for no, <c>null</c> for cancel or a dismissed dialog.</returns>
    public static async Task<bool?> ShowMessageBoxAsync(this DialogService dialogs, string? title, string message, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(MessageDialog.Message)] = message,
            [nameof(MessageDialog.YesText)] = yesText
        };

        if (noText != null)
        {
            parameters[nameof(MessageDialog.NoText)] = noText;
        }

        if (cancelText != null)
        {
            parameters[nameof(MessageDialog.CancelText)] = cancelText;
        }

        var opened = dialogs.OpenAsync<MessageDialog>(title ?? string.Empty,
                                                      parameters,
                                                      new DialogOptions
                                                      {
                                                          Width = "420px",
                                                          ShowClose = false,
                                                          CloseDialogOnEsc = false,
                                                          CloseDialogOnOverlayClick = false
                                                      });

        // While the user reads the question
        await Task.Delay(100);
        DynamicBinderWarmup.Run();

        var result = await opened;
        return result as bool?;
    }
}

// Radzen closes a dialog through dynamic calls: the first one in a session starts the C# runtime binder,
// which takes hundreds of milliseconds in the browser and froze the page on the "Yes" click
public static class DynamicBinderWarmup
{
    private static bool s_done;

    public static void Run()
    {
        if (s_done)
        {
            return;
        }

        s_done = true;
        dynamic value = true;
        dynamic task = Echo(value);
        task.ConfigureAwait(false);
    }

    private static Task Echo(dynamic value)
    {
        return Task.CompletedTask;
    }
}
