using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Radzen;

namespace Flox.Shared.Errors;

/// <summary>
/// Shows short messages to the user. Filters and the handler go through it, so how a message looks
/// (a notification, a dialog) is decided in one place and can be replaced.
/// </summary>
public interface IUiNotifier
{
    void Error(string message);

    void Warning(string message);
}

/// <summary>
/// <see cref="IUiNotifier"/> on Radzen notifications. Errors stay until the user closes them or leaves the page, so the reference
/// in the text can be read and copied.
/// </summary>
[FloxService(typeof(IUiNotifier))]
public sealed class NotificationUiNotifier : IUiNotifier, IDisposable
{
    private const double WarningDurationMs = 6000;
    private const double UntilClosedMs = 3_600_000;

    // A long word (a path, a URL) wraps: otherwise the message grows past the window and takes its close button along
    private const string MessageStyle = "max-width: min(28rem, calc(100vw - 2rem)); overflow-wrap: anywhere";

    private readonly NotificationService _notifications;
    private readonly NavigationManager _navigation;
    private readonly List<NotificationMessage> _errors = [];

    public NotificationUiNotifier(NotificationService notifications, NavigationManager navigation)
    {
        _notifications = notifications;
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
    }

    public void Dispose()
    {
        _navigation.LocationChanged -= OnLocationChanged;
    }

    public void Error(string message)
    {
        Show(message, NotificationSeverity.Error, UntilClosedMs);
    }

    public void Warning(string message)
    {
        Show(message, NotificationSeverity.Warning, WarningDurationMs);
    }

    private void Show(string message, NotificationSeverity severity, double duration)
    {
        var notification = new NotificationMessage
        {
            Severity = severity,
            Summary = message,
            Duration = duration,
            CloseOnClick = false,
            Style = MessageStyle
        };
        if (severity == NotificationSeverity.Error)
        {
            _errors.Add(notification);
        }

        _notifications.Notify(notification);
    }

    // An error belongs to the page it happened on: another page closes it
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        foreach (var error in _errors)
        {
            _notifications.Messages.Remove(error);
        }

        _errors.Clear();
    }
}
