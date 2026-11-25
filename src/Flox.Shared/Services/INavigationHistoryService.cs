namespace Flox.Services;

/// <summary>Where the user came from in this tab, to go back to (the history of the browser).</summary>
public interface INavigationHistoryService
{
    /// <summary>Whether there is a page to go back to.</summary>
    bool CanGoBack { get; }

    /// <summary>Raised when the history changes.</summary>
    event EventHandler? HistoryChanged;

    /// <summary>Navigates to the previous page.</summary>
    void GoBack();
}
