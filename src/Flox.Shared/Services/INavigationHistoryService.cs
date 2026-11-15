namespace Flox.Services;

/// <summary>The pages the user visited in this session, to go back through.</summary>
public interface INavigationHistoryService
{
    /// <summary>Whether there is a page to go back to.</summary>
    bool CanGoBack { get; }

    /// <summary>Raised when the history changes.</summary>
    event EventHandler? HistoryChanged;
    /// <summary>Forgets the visited pages.</summary>
    void ClearHistory();
    /// <summary>Navigates to the previous page.</summary>
    void GoBack();
}
