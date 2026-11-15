using System.Globalization;

namespace Flox.Sqlite;

/// <summary>The result of a query, all of it: the names of the columns and the rows (null, long, double, string or byte[]).</summary>
public sealed class SqliteRows
{
    /// <summary>A result with no columns and no rows.</summary>
    public static SqliteRows Empty { get; } = new([], []);

    /// <summary>Creates the result from the columns and the rows.</summary>
    public SqliteRows(IReadOnlyList<string> columns, IReadOnlyList<object?[]> rows)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Values = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    /// <summary>The names of the columns.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>The rows as arrays of values.</summary>
    public IReadOnlyList<object?[]> Values { get; }

    /// <summary>The number of rows.</summary>
    public int Count
    {
        get
        {
            return Values.Count;
        }
    }

    /// <summary>The row at the index.</summary>
    public SqliteRow this[int index]
    {
        get
        {
            return new SqliteRow(Values[index]);
        }
    }

    /// <summary>The rows.</summary>
    public IEnumerable<SqliteRow> Rows
    {
        get
        {
            return Values.Select(values => new SqliteRow(values));
        }
    }

    /// <summary>Returns the first column of the first row; null when there is no row.</summary>
    public object? Scalar()
    {
        return Values.Count == 0 || Values[0].Length == 0 ? null : Values[0][0];
    }
}

/// <summary>A row read by the ordinal of its column, as a data reader does.</summary>
/// <param name="Values">The values of the row.</param>
public readonly record struct SqliteRow(object?[] Values)
{
    /// <summary>The number of columns.</summary>
    public int FieldCount
    {
        get
        {
            return Values.Length;
        }
    }

    /// <summary>The raw value of the column.</summary>
    public object? this[int ordinal]
    {
        get
        {
            return Values[ordinal];
        }
    }

    /// <summary>Tells whether the value of the column is null.</summary>
    public bool IsNull(int ordinal)
    {
        return Values[ordinal] is null;
    }

    /// <summary>Reads the column as text; throws when it is null.</summary>
    public string GetString(int ordinal)
    {
        return Values[ordinal] switch
        {
            string text => text,
            null => throw new InvalidCastException($"The column {ordinal} is null."),
            var other => Convert.ToString(other, CultureInfo.InvariantCulture)!
        };
    }

    /// <summary>Reads the column as text, or null.</summary>
    public string? GetStringOrNull(int ordinal)
    {
        return Values[ordinal] is null ? null : GetString(ordinal);
    }

    /// <summary>Reads the column as a 64-bit integer; throws when it is null.</summary>
    public long GetInt64(int ordinal)
    {
        return Values[ordinal] switch
        {
            long number => number,
            double number => checked((long)number),
            string text => long.Parse(text, CultureInfo.InvariantCulture),
            null => throw new InvalidCastException($"The column {ordinal} is null."),
            var other => Convert.ToInt64(other, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Reads the column as a 32-bit integer; throws when it is null or out of range.</summary>
    public int GetInt32(int ordinal)
    {
        return checked((int)GetInt64(ordinal));
    }

    /// <summary>Reads the column as a double; throws when it is null.</summary>
    public double GetDouble(int ordinal)
    {
        return Values[ordinal] switch
        {
            double number => number,
            long number => number,
            null => throw new InvalidCastException($"The column {ordinal} is null."),
            var other => Convert.ToDouble(other, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Reads the column as a boolean: any non-zero integer is true.</summary>
    public bool GetBoolean(int ordinal)
    {
        return GetInt64(ordinal) != 0;
    }

    /// <summary>Reads the column as a blob; throws when it is not one.</summary>
    public byte[] GetBytes(int ordinal)
    {
        return Values[ordinal] as byte[] ?? throw new InvalidCastException($"The column {ordinal} is not a blob.");
    }
}
