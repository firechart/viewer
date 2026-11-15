namespace Flox.Core.Data;

/// <summary>The busy state of a data source.</summary>
public interface IDataSourceEvents
{
    /// <summary>Raised when the source goes from idle to busy and back, after <see cref="IsBusy"/> has its new value.</summary>
    event EventHandler BusyChanged;

    /// <summary>Tells whether a load or a write is running; a nested or a parallel one is the same busy period.</summary>
    bool IsBusy { get; }
}
