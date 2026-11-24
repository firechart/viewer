using System.Diagnostics.Tracing;

namespace Flox.Shared.Components;

/// <summary>
/// Shortcuts of widgets and states for dialogs, confirmations, error messages and snack messages.
/// </summary>
public static class WidgetExtensions
{
    #region ShowMessageAsync

    /// <summary>
    /// Shows a message dialog; the title is the level, or the information caption without one. Returns true for yes, false for no, null for cancel.
    /// </summary>
    public static async Task<bool?> ShowMessageAsync(this StatelessWidget sender, string message, EventLevel? eventLevel = null, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        var title = eventLevel == null ? Strings.Caption_Information : eventLevel.ToString();
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, yesText, noText, cancelText);
        return result;
    }

    /// <summary>
    /// Shows a message dialog; the title is the level, or the information caption without one. Returns true when the user chose yes.
    /// </summary>
    public static async Task<bool> ShowMessageAsync(this State sender, string message, EventLevel? eventLevel = null, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        var title = eventLevel == null ? Strings.Caption_Information : eventLevel.ToString();
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, yesText, noText, cancelText);
        return true == result;
    }

    /// <summary>
    /// Asks the user to confirm; true when confirmed.
    /// </summary>
    public static async Task<bool> ConfirmAsync(this StatelessWidget sender, string message)
    {
        var title = Strings.Caption_Confirm;
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, Strings.Caption_Yes, Strings.Caption_No);
        return true == result;
    }

    /// <summary>
    /// Asks the user to confirm; true when confirmed, false when declined, null when dismissed.
    /// </summary>
    public static async Task<bool?> ConfirmAsync(this State sender, string message)
    {
        var title = Strings.Caption_Confirm;
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, Strings.Caption_Yes, Strings.Caption_No);
        return result;
    }

    #endregion ShowMessageAsync

    #region ShowErrorAsync

    /// <summary>
    /// Shows the message as an error dialog.
    /// </summary>
    public static async Task ShowErrorAsync(this StatelessWidget sender, string message)
    {
        await sender.ShowMessageAsync(message, EventLevel.Error);
    }

    /// <summary>
    /// Shows the message as an error dialog.
    /// </summary>
    public static async Task ShowErrorAsync(this State sender, string message)
    {
        await sender.ShowMessageAsync(message, EventLevel.Error);
    }

    /// <summary>
    /// Shows <paramref name="ex"/> through the UI exception handler (see <c>Flox.Shared.Errors</c>).
    /// Prefer <c>TryAsync</c>/<c>RunAsync</c>: exceptions of event handlers are handled without a call.
    /// </summary>
    /// <param name="sender">The widget that shows the error.</param>
    /// <param name="message">Text for the user instead of the standard one.</param>
    /// <param name="ex">The exception to show; one is made of the message when null.</param>
    public static Task HandleExceptionUI(this StatelessWidget sender, string? message = null, Exception? ex = null)
    {
        return sender.UiErrors.HandleAsync(ex ?? new FloxException(message),
                                           sender.GetType()
                                                 .Name,
                                           userMessage: message);
    }

    /// <summary>
    /// Shows the exception, or the message, through the UI exception handler.
    /// </summary>
    public static Task HandleExceptionUI(this State sender, string? message = null, Exception? ex = null)
    {
        return sender.UiErrors.HandleAsync(ex ?? new FloxException(message),
                                           sender.GetType()
                                                 .Name,
                                           userMessage: message);
    }

    #endregion ShowErrorAsync

    #region ShowSnackAsync

    /// <summary>
    /// Shows a snack message.
    /// </summary>
    public static void ShowSnack(this StatelessWidget sender, string message, SnackSeverity severity = SnackSeverity.Info)
    {
        sender.Notifications.Show(message, severity);
    }

    /// <summary>
    /// Shows a snack message.
    /// </summary>
    public static void ShowSnack(this State sender, string message, SnackSeverity severity = SnackSeverity.Info)
    {
        sender.Notifications.Show(message, severity);
    }

    #endregion ShowSnackAsync

    /// <summary>
    /// Opens the dialog component as a modal dialog and returns its result: the model on OK, a canceled result otherwise.
    /// </summary>
    public static async Task<ModalDialogResult<TModel>> ShowDialogAsync<TDialog, TModel>(this StatelessWidget sender, ModalDialogMode mode, TModel? model = null, Dictionary<string, object?>? dialogParameters = null, Func<FloxDialogOptions>? configureDialog = null, string? title = null) where TDialog : ModalDialogWidget<TModel>, IComponent where TModel : class
    {
        dialogParameters ??= [];
        dialogParameters.TryAdd(nameof(ModalDialogWidget<TModel>.Mode), mode);
        if (!dialogParameters.ContainsKey(nameof(ModalDialogWidget<TModel>.Model)) && model != null)
        {
            dialogParameters[nameof(ModalDialogWidget<TModel>.Model)] = model;
        }

        var dialogOptions = configureDialog != null
            ? configureDialog()
            : new FloxDialogOptions
            {
                Width = "960px",
                ShowClose = true,
                CloseOnEscape = true
            };

        object? data = await sender.DialogService.OpenAsync<TDialog>(title ?? string.Empty, dialogParameters, dialogOptions);
        return new ModalDialogResult<TModel>(data as TModel)
        {
            Mode = mode
        };
    }
}
