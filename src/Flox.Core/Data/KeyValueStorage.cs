using System.Text.Json;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Data;

/// <summary>
/// Small values a component keeps on the device under a key of its own (a toggle, the width of a pane, the last tab): the state of the interface,
/// never a setting of the user and never sent to a server.
/// </summary>
public interface IKeyValueStorage
{
    /// <summary>Reads the value of the key, or <paramref name="defaultValue"/> when there is none, it does not read as a <typeparamref name="T"/>, or
    /// the device keeps nothing here.</summary>
    ValueTask<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>Keeps the value of the key; where the device keeps nothing, the value is lost and the component works on.</summary>
    ValueTask SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IKeyValueStorage"/> in the documents of the device (<see cref="ILocalDocumentStore"/>, SQLite of the browser): one collection, a
/// document per key, the value as JSON. Scoped: the collection is read once and kept in memory, and read again when another tab changed it.
/// </summary>
[FloxService(typeof(IKeyValueStorage), ServiceLifetime.Scoped)]
public sealed class LocalKeyValueStorage : IKeyValueStorage, IDisposable
{
    /// <summary>The collection of the values in the <see cref="ILocalDocumentStore"/>.</summary>
    public const string Collection = "FloxUiState";

    private readonly ILocalDocumentStore _store;
    private Dictionary<string, string>? _values;

    /// <summary>Creates the storage over the documents of the device.</summary>
    public LocalKeyValueStorage(ILocalDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.ChangedElsewhere += OnChangedElsewhere;
    }

    /// <inheritdoc />
    public async ValueTask<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            var values = await ValuesAsync(cancellationToken);
            return values.TryGetValue(key, out var json) && JsonSerializer.Deserialize<T>(json) is { } value ? value : defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async ValueTask SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var json = JsonSerializer.Serialize(value);
        try
        {
            var values = await ValuesAsync(cancellationToken);
            if (values.TryGetValue(key, out var kept) && kept == json)
            {
                return;
            }

            await _store.PutAsync(Collection, [new LocalDocument(key, json)], cancellationToken);
            values[key] = json;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // nothing is kept: the component works without it
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _store.ChangedElsewhere -= OnChangedElsewhere;
    }

    private async ValueTask<Dictionary<string, string>> ValuesAsync(CancellationToken cancellationToken)
    {
        if (_values is { } values)
        {
            return values;
        }

        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in await _store.LoadAsync(Collection, cancellationToken))
        {
            read[document.Key] = document.Json;
        }

        _values = read;
        return read;
    }

    private void OnChangedElsewhere(object? sender, LocalDocumentsChangedEventArgs e)
    {
        if (e.Concerns(Collection))
        {
            _values = null;
        }
    }

    // No store here (prerendering, a closed page: the errors of JavaScript interop) or a store that failed
    private static bool IsUnavailable(Exception ex)
    {
        return ex is InvalidOperationException || ex.GetType().Namespace == "Microsoft.JSInterop";
    }
}
