using System.Globalization;

namespace Flox.Sqlite;

/// <summary>The values of .NET as SQLite keeps them, the same for every engine.</summary>
public static class SqliteValues
{
    /// <summary>
    /// Turns the value of a parameter into null, long, double, string or byte[]: an integer of any size into long, a bool into 0 or 1, a float or a
    /// decimal into double, a Guid into its lower-case text, an enum into its name, a date into ISO 8601 (a DateTimeOffset in UTC), so that texts
    /// compare as the dates do.
    /// </summary>
    /// <exception cref="ArgumentException">The type has no form in SQLite.</exception>
    public static object? Normalize(object? value)
    {
        return value switch
        {
            null or string or long or double or byte[] => value,
            bool flag => flag ? 1L : 0L,
            int number => (long)number,
            short number => (long)number,
            byte number => (long)number,
            sbyte number => (long)number,
            ushort number => (long)number,
            uint number => (long)number,
            ulong number => checked((long)number),
            float number => (double)number,
            decimal number => (double)number,
            char character => character.ToString(),
            Guid key => key.ToString(),
            Enum named => named.ToString(),
            DateTimeOffset moment => moment.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTime moment => moment.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"A value of the type {value.GetType().Name} cannot be a parameter of SQLite.", nameof(value))
        };
    }
}
