using System.IO.Compression;
using Flox.Sqlite;
using Microsoft.Data.Sqlite;

namespace Flox.Sqlite.Files;

/// <summary>
/// The SQLite bases of a folder through Microsoft.Data.Sqlite: the engine of the tests on .NET, of the server and of the tools (the browser has its own
/// in Flox.Sqlite). A base is the file &lt;folder&gt;/&lt;name&gt;; one connection per open base, its commands one at a time.
/// <see cref="ImportAsync"/> downloads with the <see cref="HttpClient"/> given.
/// </summary>
public sealed class FileSqliteEngine : ISqliteEngine
{
    private const string Partial = ".partial";
    private static readonly string[] s_companions = ["-journal", "-wal", "-shm", Partial];

    private readonly string _folder;
    private readonly HttpClient? _http;
    private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>
    /// Creates an engine over a folder.
    /// </summary>
    /// <param name="folder">The folder that holds the bases.</param>
    /// <param name="http">The client that <see cref="ImportAsync"/> downloads with, or null when nothing is imported.</param>
    public FileSqliteEngine(string folder, HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = Path.GetFullPath(folder);
        _http = http;
    }

    /// <inheritdoc />
    public Task<ISqliteDatabase> OpenAsync(string name, SqliteAccess access, CancellationToken cancellationToken = default)
    {
        var path = PathOf(name);
        if (access == SqliteAccess.ReadOnly && !File.Exists(path))
        {
            throw new SqliteEngineException($"The base {name} does not exist.", 14);
        }

        Directory.CreateDirectory(_folder);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = access == SqliteAccess.ReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
        }
        catch (SqliteException exception)
        {
            connection.Dispose();
            throw new SqliteEngineException(exception.Message, exception.SqliteErrorCode, exception);
        }

        lock (_lock)
        {
            _open.Add(name);
        }

        return Task.FromResult<ISqliteDatabase>(new FileSqliteDatabase(name, access, connection, () =>
        {
            lock (_lock)
            {
                _open.Remove(name);
            }
        }));
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(PathOf(name)));
    }

    /// <inheritdoc />
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_open.Contains(name))
            {
                throw new SqliteEngineException($"The base {name} is open.", 5);
            }
        }

        var path = PathOf(name);
        foreach (var file in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> names = Directory.Exists(_folder)
            ? Directory.EnumerateFiles(_folder)
                       .Select(Path.GetFileName)
                       .OfType<string>()
                       .Where(name => !s_companions.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                       .Order(StringComparer.Ordinal)
                       .ToList()
            : [];
        return Task.FromResult(names);
    }

    /// <inheritdoc />
    public async Task<long> ImportAsync(string name, Uri source, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var http = _http ?? throw new InvalidOperationException("The engine has no HttpClient to download with.");
        var path = PathOf(name);
        var partial = path + Partial;
        Directory.CreateDirectory(_folder);
        try
        {
            using var response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            long written;
            await using (var body = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var counted = new CountingStream(body, progress))
            await using (var gzip = new GZipStream(counted, CompressionMode.Decompress))
            await using (var target = File.Create(partial))
            {
                await gzip.CopyToAsync(target, cancellationToken);
                written = target.Length;
            }

            if (written < 512 || written % 512 != 0 || !HasHeader(partial))
            {
                throw new SqliteEngineException($"{source} is not a compressed SQLite base.", 26);
            }

            File.Move(partial, path, true);
            return written;
        }
        catch (InvalidDataException exception)
        {
            throw new SqliteEngineException($"{source} is not a compressed SQLite base: {exception.Message}", 26, exception);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private static bool HasHeader(string path)
    {
        Span<byte> header = stackalloc byte[16];
        using var file = File.OpenRead(path);
        return file.Read(header) == 16 && header.SequenceEqual("SQLite format 3\0"u8);
    }

    // A name of a base, not a path: no folder, no "..", no wildcard
    private string PathOf(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(['/', '\\', ':', '*', '?']) >= 0 || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' is not the name of a base.", nameof(name));
        }

        return Path.Combine(_folder, name);
    }
}

internal sealed class FileSqliteDatabase(string name, SqliteAccess access, SqliteConnection connection, Action closed) : ISqliteDatabase
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public string Name { get; } = name;

    public SqliteAccess Access { get; } = access;

    public Task<SqliteRows> QueryAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            using var command = Command(sql, parameters);
            using var reader = command.ExecuteReader();
            var columns = Enumerable.Range(0, reader.FieldCount)
                                    .Select(reader.GetName)
                                    .ToList();
            var rows = new List<object?[]>();
            while (reader.Read())
            {
                var values = new object?[reader.FieldCount];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(values);
            }

            return new SqliteRows(columns, rows);
        }, cancellationToken);
    }

    public Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            using var command = Command(sql, parameters);
            return command.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<int>> ExecuteBatchAsync(IReadOnlyList<SqliteStatement> statements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        return RunAsync<IReadOnlyList<int>>(() =>
        {
            using var transaction = connection.BeginTransaction();
            var changed = new List<int>(statements.Count);
            foreach (var statement in statements)
            {
                using var command = Command(statement.Sql, statement.Parameters);
                command.Transaction = transaction;
                changed.Add(command.ExecuteNonQuery());
            }

            transaction.Commit();
            return changed;
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await connection.DisposeAsync();
            closed();
        }
        finally
        {
            _gate.Release();
        }

        // A call that comes later finds it disposed (ObjectDisposedException)
        _gate.Dispose();
    }

    private async Task<T> RunAsync<T>(Func<T> run, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return run();
        }
        catch (SqliteException exception)
        {
            throw new SqliteEngineException(exception.Message, exception.SqliteErrorCode, exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private SqliteCommand Command(string sql, IReadOnlyDictionary<string, object?>? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var command = connection.CreateCommand();
#pragma warning disable CA2100 // the SQL of the callers; the values are parameters
        command.CommandText = sql;
#pragma warning restore CA2100
        foreach (var (key, value) in parameters ?? new Dictionary<string, object?>())
        {
            command.Parameters.AddWithValue(key, SqliteValues.Normalize(value) ?? DBNull.Value);
        }

        return command;
    }
}

// The bytes read through it, reported as they come
internal sealed class CountingStream(Stream inner, IProgress<long>? progress) : Stream
{
    private long _count;

    public override bool CanRead
    {
        get
        {
            return true;
        }
    }

    public override bool CanSeek
    {
        get
        {
            return false;
        }
    }

    public override bool CanWrite
    {
        get
        {
            return false;
        }
    }

    public override long Length
    {
        get
        {
            throw new NotSupportedException();
        }
    }

    public override long Position
    {
        get
        {
            return _count;
        }
        set
        {
            throw new NotSupportedException();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Counted(inner.Read(buffer, offset, count));
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Counted(await inner.ReadAsync(buffer, cancellationToken));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private int Counted(int read)
    {
        if (read > 0)
        {
            _count += read;
            progress?.Report(_count);
        }

        return read;
    }
}
