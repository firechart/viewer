namespace Flox.Shared.Components;

/// <summary>
/// Extensions of the dialog service for message boxes.
/// </summary>
public static class DialogExtensions
{
    /// <summary>
    /// Shows a message with up to three answers.
    /// </summary>
    /// <returns><c>true</c> for yes/OK, <c>false</c> for no, <c>null</c> for cancel or a dismissed dialog.</returns>
    public static async Task<bool?> ShowMessageBoxAsync(this FloxDialogService dialogs, string? title, string message, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
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

        var result = await dialogs.OpenAsync<MessageDialog>(title ?? string.Empty,
                                                            parameters,
                                                            new FloxDialogOptions
                                                            {
                                                                Width = "420px",
                                                                ShowClose = false,
                                                                CloseOnEscape = false,
                                                                CloseOnOverlayClick = false
                                                            });
        return result as bool?;
    }
}
