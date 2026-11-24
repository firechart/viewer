using Microsoft.AspNetCore.Components;
using Flox.Shared.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Flox.Shared.Errors;

/// <summary>
/// Shows short messages to the user. Filters and the handler go through it, so how a message looks
/// (a notification, a dialog) is decided in one place and can be replaced.
/// </summary>
public interface IUiNotifier
{
    /// <summary>Shows an error message that stays until the user dismisses it.</summary>
    void Error(string message);

    /// <summary>Shows a warning message that disappears by itself.</summary>
    void Warning(string message);
}

/// <summary>
/// <see cref="IUiNotifier"/> on the snack messages of Flox. Errors stay until the user closes them or leaves the page, so the reference
/// in the text can be read and copied.
/// </summary>
[FloxService(typeof(IUiNotifier))]
public sealed class NotificationUiNotifier : IUiNotifier, IDisposable
{
    private const int WarningDurationMs = 6000;

    private readonly FloxSnackbar _notifications;
    private readonly NavigationManager _navigation;
    private readonly List<string> _errors = [];

    /// <summary>Creates the notifier over the snack messages and the navigation manager used to close errors when the page changes.</summary>
    public NotificationUiNotifier(FloxSnackbar notifications, NavigationManager navigation)
    {
        _notifications = notifications;
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigation.LocationChanged -= OnLocationChanged;
    }

    /// <inheritdoc />
    public void Error(string message)
    {
        _errors.Add(_notifications.Show(message, SnackSeverity.Error, 0));
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        _notifications.Show(message, SnackSeverity.Warning, WarningDurationMs);
    }

    // An error belongs to the page it happened on: another page closes it
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        foreach (var error in _errors)
        {
            _notifications.Dismiss(error);
        }

        _errors.Clear();
    }
}
