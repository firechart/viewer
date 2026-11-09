using System.Diagnostics.Tracing;
using Radzen;

namespace Flox.Shared.Components;

public static class WidgetExtensions
{
    #region ShowMessageAsync

    public static async Task<bool?> ShowMessageAsync(this StatelessWidget sender, string message, EventLevel? eventLevel = null, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        var title = eventLevel == null ? Strings.Caption_Information : eventLevel.ToString();
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, yesText, noText, cancelText);
        return result;
    }

    public static async Task<bool> ShowMessageAsync(this State sender, string message, EventLevel? eventLevel = null, string yesText = "OK", string? noText = null, string? cancelText = null)
    {
        var title = eventLevel == null ? Strings.Caption_Information : eventLevel.ToString();
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, yesText, noText, cancelText);
        return true == result;
    }

    public static async Task<bool> ConfirmAsync(this StatelessWidget sender, string message)
    {
        var title = Strings.Caption_Confirm;
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, Strings.Caption_Yes, Strings.Caption_No);
        return true == result;
    }

    public static async Task<bool?> ConfirmAsync(this State sender, string message)
    {
        var title = Strings.Caption_Confirm;
        var result = await sender.DialogService.ShowMessageBoxAsync(title, message, Strings.Caption_Yes, Strings.Caption_No);
        return result;
    }

    #endregion ShowMessageAsync

    #region ShowErrorAsync

    public static async Task ShowErrorAsync(this StatelessWidget sender, string message)
    {
        await sender.ShowMessageAsync(message, EventLevel.Error);
    }

    public static async Task ShowErrorAsync(this State sender, string message)
    {
        await sender.ShowMessageAsync(message, EventLevel.Error);
    }

    /// <summary>
    /// Shows <paramref name="ex"/> through the UI exception handler (see <c>Flox.Shared.Errors</c>).
    /// Prefer <c>TryAsync</c>/<c>RunAsync</c>: exceptions of event handlers are handled without a call.
    /// </summary>
    /// <param name="message">Text for the user instead of the standard one.</param>
    public static Task HandleExceptionUI(this StatelessWidget sender, string? message = null, Exception? ex = null)
    {
        return sender.UiErrors.HandleAsync(ex ?? new FloxException(message),
                                           sender.GetType()
                                                 .Name,
                                           userMessage: message);
    }

    public static Task HandleExceptionUI(this State sender, string? message = null, Exception? ex = null)
    {
        return sender.UiErrors.HandleAsync(ex ?? new FloxException(message),
                                           sender.GetType()
                                                 .Name,
                                           userMessage: message);
    }

    #endregion ShowErrorAsync

    #region ShowSnackAsync

    public static void ShowSnack(this StatelessWidget sender, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        sender.Notifications.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = message
        });
    }

    public static void ShowSnack(this State sender, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        sender.Notifications.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = message
        });
    }

    #endregion ShowSnackAsync

    public static async Task<ModalDialogResult<TModel>> ShowDialogAsync<TDialog, TModel>(this StatelessWidget sender, ModalDialogMode mode, TModel? model = null, Dictionary<string, object?>? dialogParameters = null, Func<DialogOptions>? configureDialog = null, string? title = null) where TDialog : ModalDialogWidget<TModel>, IComponent where TModel : class
    {
        dialogParameters ??= [];
        dialogParameters.TryAdd(nameof(ModalDialogWidget<TModel>.Mode), mode);
        if (!dialogParameters.ContainsKey(nameof(ModalDialogWidget<TModel>.Model)) && model != null)
        {
            dialogParameters[nameof(ModalDialogWidget<TModel>.Model)] = model;
        }

        var dialogOptions = configureDialog != null
            ? configureDialog()
            : new DialogOptions
            {
                Width = "960px",
                ShowClose = true,
                CloseDialogOnEsc = true
            };

        object? data = await sender.DialogService.OpenAsync<TDialog>(title ?? string.Empty, dialogParameters, dialogOptions);
        return new ModalDialogResult<TModel>(data as TModel)
        {
            Mode = mode
        };
    }
}
