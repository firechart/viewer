namespace Flox.Services;

public interface INavigationHistoryService
{
    bool CanGoBack { get; }

    event EventHandler? HistoryChanged;
    void ClearHistory();
    void GoBack();
}
