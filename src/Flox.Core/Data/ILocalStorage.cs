namespace Flox.Core.Data;

/// <summary>
/// A key-value store of the device (browser local storage). Not to be confused with <see cref="IStorage{TKey, TEntity}"/>: they are completely
/// different.
/// </summary>
public interface ILocalStorage
{
    /// <summary>The name of the underlying store.</summary>
    string StorageName { get; }

    /// <summary>Returns all the keys in the store.</summary>
    Task<List<string>> GetAllKeysAsync();

    /// <summary>Returns the value of the key, or null when there is none.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Removes the key.</summary>
    Task RemoveAsync(string key);

    /// <summary>Stores the value under the key; null removes it.</summary>
    Task SetAsync(string key, string? value);
}
