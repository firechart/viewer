
namespace Flox.Sqlite;

/// <summary>
/// A property of an entity as an expression of SQL; <paramref name="Value"/> turns the value of a filter into the stored one (a name into its code, a bool into 0/1, a key
/// into its rowid).
/// </summary>
/// <param name="Sql">The SQL expression of the property.</param>
/// <param name="Value">Converts the value of a filter into the stored one, or null when it is used as is.</param>
public sealed record SqliteColumn(string Sql, Func<object?, object?>? Value = null)
{
    /// <summary>
    /// Turns a bool of a filter into the integer SQLite keeps; any other value stays as it is.
    /// </summary>
    public static object? Flag(object? value)
    {
        return value is bool b ? (b ? 1L : 0L) : value;
    }
}

/// <summary>
/// How an entity is read from a base: the tables (FROM, joins included), the rowid of a row, the columns of the SELECT that <see cref="Read"/> gets in this order,
/// the properties that filters and sorts may use as SQL, and the full-text condition. A property that is not a column is filtered and sorted in memory.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class SqliteMapping<T> where T : class
{
    /// <summary>
    /// The number of the table, which goes into the keys of the rows with the rowid: the mappings of one base differ in it.
    /// </summary>
    public required short Table { get; init; }

    /// <summary>The FROM clause: the tables, joins included.</summary>
    public required string From { get; init; }

    /// <summary>The SQL expression of the rowid of a row.</summary>
    public required string RowId { get; init; }

    /// <summary>The columns of the SELECT, in the order <see cref="Read"/> gets them.</summary>
    public required string Select { get; init; }

    /// <summary>Makes an entity from a row.</summary>
    public required Func<SqliteRow, SqliteRead, T> Read { get; init; }

    /// <summary>The properties that filters and sorts may use as SQL, by name.</summary>
    public IReadOnlyDictionary<string, SqliteColumn> Columns { get; init; } = new Dictionary<string, SqliteColumn>(StringComparer.Ordinal);

    /// <summary>
    /// The condition of the full-text search: $fts is the query of FTS5 ("word"* for each word); null: the search is done in memory over the strings.
    /// </summary>
    public string? FullText { get; init; }

    /// <summary>
    /// The properties that are expensive to read (a JSON): a request whose Fields name none of them does not read them (<see cref="SqliteRead.Full"/> false).
    /// </summary>
    public IReadOnlySet<string> LargeFields { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whether the rows are read again by their key (a table WITHOUT ROWID is not).
    /// </summary>
    public bool Addressable { get; init; } = true;
}

/// <summary>
/// How a row is read.
/// </summary>
/// <param name="Full">False when the request does not need the large fields.</param>
public readonly record struct SqliteRead(bool Full);

/// <summary>
/// The key of a row: the table of its mapping and its rowid. Valid while the base is the same.
/// </summary>
public static class SqliteKeys
{
    private const short Mark = 0x5351;

    /// <summary>
    /// Makes the key of a row of the only base.
    /// </summary>
    public static Guid Of(short table, long rowId)
    {
        return Of(0, table, rowId);
    }

    /// <summary>
    /// Makes the key of a row of one of several bases of the same schema.
    /// </summary>
    /// <param name="base">Which of the bases holds the row (their rowids repeat).</param>
    /// <param name="table">The table of the mapping.</param>
    /// <param name="rowId">The rowid of the row.</param>
    public static Guid Of(int @base, short table, long rowId)
    {
        return new Guid(@base, table, Mark, BitConverter.GetBytes(rowId));
    }

    /// <summary>
    /// Splits a key into its parts; null when the Guid is not a key of a row.
    /// </summary>
    public static SqliteKey? Parse(Guid key)
    {
        var bytes = key.ToByteArray();
        if (BitConverter.ToInt16(bytes, 6) != Mark)
        {
            return null;
        }

        return new SqliteKey(BitConverter.ToInt32(bytes, 0), BitConverter.ToInt16(bytes, 4), BitConverter.ToInt64(bytes, 8));
    }

    /// <summary>
    /// Gets the rowid of a key of the table, for a filter: -1 (no row) when the value is not one.
    /// </summary>
    public static object? RowIdOf(short table, object? value)
    {
        return value is Guid key && Parse(key) is { } parsed && parsed.Table == table ? parsed.RowId : -1L;
    }
}

/// <summary>
/// The parts of the key of a row.
/// </summary>
/// <param name="Base">Which of several bases holds the row.</param>
/// <param name="Table">The table of the mapping.</param>
/// <param name="RowId">The rowid of the row.</param>
public readonly record struct SqliteKey(int Base, short Table, long RowId);
