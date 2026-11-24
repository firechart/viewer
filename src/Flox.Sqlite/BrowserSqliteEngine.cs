using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;

namespace Flox.Sqlite;

/// <summary>
/// The SQLite bases of the browser client: sqlite-wasm in a dedicated worker over OPFS, one pool per application (its name keeps Pro and Lite of one origin apart).
/// Registered by the application that uses it, not discovered. The pool holds its files exclusively, so the tabs and frames of one pool share one worker: the tab
/// holding the Web Lock of the pool runs it and answers the others over a BroadcastChannel, and the lead moves when that tab closes (flox-sqlite.js).
/// </summary>
public sealed partial class BrowserSqliteEngine(IJSRuntime runtime, string pool = BrowserSqliteEngine.DefaultPool, int capacity = BrowserSqliteEngine.DefaultCapacity)
    : ISqliteEngine, IAsyncDisposable
{
    /// <summary>Name of the pool when the application gives none.</summary>
    public const string DefaultPool = "flox";

    /// <summary>Number of files a pool may hold: every base takes its file and, while it writes, its journal.</summary>
    public const int DefaultCapacity = 16;

    private const string ModulePath = "./_content/Flox.Sqlite/flox-sqlite.js";

    // Why two calls of this engine are tried again (ImportAsync, OpenAsync). The interop of Blazor WebAssembly now and then answers a call with null
    // although the script returned a value: the module of an import, the number of the handle of an open (the script cannot return null there).
    // Seen with the browser extension Urban VPN Proxy, under the debugger of Visual Studio, once in the browser pane of Claude, and in production
    // without either (demo.firechart.org, 2026-11-21: the home page of Pro opening flox.sqlite for its bookmarks and recent pages); the cause is
    // not found, and the same call made again succeeds (delivery-pitfalls.md 9.10 and 9.11). Only a call that may run twice is tried again: an
    // import, and an open (an open made twice gives a second handle of the same base; the first, if the script made it, stays open in this tab
    // until the page goes, which costs nothing). A query, an execute or a batch is never tried again: a write would be done twice.
    // Attempts pause 100, 200, 300 ms (0.6 s at most), then fail with a message that names the likely cause.
    private const int Attempts = 4;
    private static readonly TimeSpan s_pause = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async Task<ISqliteDatabase> OpenAsync(string name, SqliteAccess access, CancellationToken cancellationToken = default)
    {
        Validate(name);
        var module = await ModuleAsync(cancellationToken);
        // An empty answer of the interop is tried again: see Attempts
        for (var attempt = 1; ; attempt++)
        {
            if (await CallAsync(() => module.InvokeAsync<int?>("open", cancellationToken, name, access == SqliteAccess.ReadOnly)) is { } handle)
            {
                return new BrowserSqliteDatabase(name, access, module, handle);
            }

            if (attempt == Attempts)
            {
                throw new SqliteEngineException($"The base {name} came back without a handle from the interop {Attempts} times: a browser extension or the debugger may break the interop of the page.");
            }

            await Task.Delay(s_pause * attempt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        Validate(name);
        var module = await ModuleAsync(cancellationToken);
        return await CallAsync(() => module.InvokeAsync<bool>("exists", cancellationToken, name));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        Validate(name);
        var module = await ModuleAsync(cancellationToken);
        await CallAsync(() => module.InvokeAsync<bool>("remove", cancellationToken, name));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync(cancellationToken);
        return await CallAsync(() => module.InvokeAsync<string[]>("list", cancellationToken));
    }

    /// <summary>
    /// Imports a base from <paramref name="source"/>: the worker fetches and unpacks it straight into OPFS, so it never passes through the memory
    /// of .NET. Returns its size. A cancellation stops the download in the worker, and the call ends when nothing of the base is left.
    /// </summary>
    public async Task<long> ImportAsync(string name, Uri source, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        Validate(name);
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsAbsoluteUri)
        {
            throw new ArgumentException("The worker fetches an absolute address.", nameof(source));
        }

        var module = await ModuleAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var reporter = DotNetObjectReference.Create(new ImportProgress(progress));
        // The call is not abandoned on a cancellation: the worker is told to stop, and its answer is awaited
        var importing = CallAsync(() => module.InvokeAsync<long>("importGzip", CancellationToken.None, name, source.AbsoluteUri, reporter));
        await using var cancelling = cancellationToken.Register(() => _ = CancelImportAsync(module, name));
        try
        {
            return await importing;
        }
        catch (SqliteEngineException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static async Task CancelImportAsync(IJSObjectReference module, string name)
    {
        try
        {
            await module.InvokeAsync<bool>("cancelImport", name);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // the import ends by itself
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the page is gone
            }
        }

        _gate.Dispose();
    }

    // The worker and its pool once, for every caller
    private async Task<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_module is null)
            {
                var module = await ImportAsync(cancellationToken);
                await CallAsync(() => module.InvokeAsync<string>("init", cancellationToken, pool, capacity));
                _module = module;
            }

            return _module;
        }
        finally
        {
            _gate.Release();
        }
    }

    // An empty answer of the interop is tried again: see Attempts (as JSModuleImport of Flox.Shared, which this library does not reference)
    private async Task<IJSObjectReference> ImportAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (await runtime.InvokeAsync<IJSObjectReference?>("import", cancellationToken, ModulePath) is { } module)
            {
                return module;
            }

            if (attempt == Attempts)
            {
                throw new SqliteEngineException($"The script {ModulePath} came back empty from the interop {Attempts} times: a browser extension or the debugger may break the interop of the page.");
            }

            await Task.Delay(s_pause * attempt, cancellationToken);
        }
    }

    private static void Validate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(['/', '\\', ':', '*', '?']) >= 0 || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' is not the name of a base.", nameof(name));
        }
    }

    // An error of the worker comes as "[code] message"
    internal static async Task<T> CallAsync<T>(Func<ValueTask<T>> call)
    {
        try
        {
            return await call();
        }
        catch (JSException exception)
        {
            var match = WorkerError().Match(exception.Message);
            throw match.Success
                ? new SqliteEngineException(match.Groups[2].Value, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), exception)
                : new SqliteEngineException(exception.Message, 0, exception);
        }
    }

    [GeneratedRegex(@"^\[(\d+)\] ([^\n]*)")]
    private static partial Regex WorkerError();
}

// The compressed bytes received, reported by the worker
internal sealed class ImportProgress(IProgress<long>? progress)
{
    [JSInvokable]
    public void Report(long received)
    {
        progress?.Report(received);
    }
}

internal sealed class BrowserSqliteDatabase(string name, SqliteAccess access, IJSObjectReference module, int handle) : ISqliteDatabase
{
    private bool _closed;

    public string Name { get; } = name;

    public SqliteAccess Access { get; } = access;

    public async Task<SqliteRows> QueryAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var text = await BrowserSqliteEngine.CallAsync(() => module.InvokeAsync<byte[]>("query", cancellationToken, Open(), sql, SqliteWire.Parameters(parameters)));
        return SqliteWire.Rows(text);
    }

    public Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        return BrowserSqliteEngine.CallAsync(() => module.InvokeAsync<int>("execute", cancellationToken, Open(), sql, SqliteWire.Parameters(parameters)));
    }

    public async Task<IReadOnlyList<int>> ExecuteBatchAsync(IReadOnlyList<SqliteStatement> statements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        return await BrowserSqliteEngine.CallAsync(() => module.InvokeAsync<int[]>("batch", cancellationToken, Open(), SqliteWire.Statements(statements)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        try
        {
            await module.InvokeAsync<bool>("close", handle);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or ObjectDisposedException)
        {
            // the page is gone, and the worker with it; or the leading tab has nothing to close
        }
    }

    private int Open()
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        return handle;
    }
}

// The JSON text that crosses into the worker: written and read by hand (the serializer of the interop is slow for thousands of values); the rows
// come back as its UTF-8 bytes, which the interop passes as they are. A blob is {"b64":...}, an integer beyond the safe range of JavaScript
// {"i64":"..."}
internal static class SqliteWire
{
    private const long SafeInteger = 9_007_199_254_740_991L;

    public static string? Parameters(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteParameters(writer, parameters);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static string Statements(IReadOnlyList<SqliteStatement> statements)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var statement in statements)
            {
                writer.WriteStartObject();
                writer.WriteString("sql", statement.Sql);
                if (statement.Parameters is { Count: > 0 })
                {
                    writer.WritePropertyName("parameters");
                    WriteParameters(writer, statement.Parameters);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static SqliteRows Rows(ReadOnlySpan<byte> text)
    {
        var reader = new Utf8JsonReader(text);
        var columns = new List<string>();
        var rows = new List<object?[]>();
        Read(ref reader, JsonTokenType.StartObject);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var property = reader.GetString();
            Read(ref reader, JsonTokenType.StartArray);
            if (property == "columns")
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    columns.Add(reader.GetString()!);
                }
            }
            else
            {
                var row = new List<object?>(columns.Count);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    row.Clear();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        row.Add(Value(ref reader));
                    }

                    rows.Add([.. row]);
                }
            }
        }

        return new SqliteRows(columns, rows);
    }

    private static void WriteParameters(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> parameters)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in parameters)
        {
            writer.WritePropertyName(key);
            switch (SqliteValues.Normalize(value))
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case byte[] bytes:
                    writer.WriteStartObject();
                    writer.WriteBase64String("b64", bytes);
                    writer.WriteEndObject();
                    break;
                case long number when number is > SafeInteger or < -SafeInteger:
                    writer.WriteStartObject();
                    writer.WriteString("i64", number.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndObject();
                    break;
                case long number:
                    writer.WriteNumberValue(number);
                    break;
                case double number:
                    writer.WriteNumberValue(number);
                    break;
            }
        }

        writer.WriteEndObject();
    }

    private static object? Value(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                // Boxed apart: a ternary of long and double would make every integer a double
                if (reader.ValueSpan.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0 && reader.TryGetInt64(out var integer))
                {
                    return integer;
                }

                return reader.GetDouble();
            case JsonTokenType.StartObject:
                object? value = null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var kind = reader.GetString();
                    reader.Read();
                    value = kind == "b64" ? reader.GetBytesFromBase64() : long.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
                }

                return value;
            default:
                throw new SqliteEngineException($"A value of SQLite cannot be {reader.TokenType}.");
        }
    }

    private static void Read(ref Utf8JsonReader reader, JsonTokenType expected)
    {
        if (!reader.Read() || reader.TokenType != expected)
        {
            throw new SqliteEngineException($"The answer of the worker is not what it should be ({expected} expected, {reader.TokenType} found).");
        }
    }
}
