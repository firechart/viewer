namespace Flox.Core.Data;

public interface IDataSourceEvents
{
    // Raised when the source goes from idle to busy and back, after IsBusy has its new value
    event EventHandler BusyChanged;

    // A load or a write is running (a nested or a parallel one is the same busy period)
    bool IsBusy { get; }
}
