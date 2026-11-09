namespace Flox.Core.Data;

// Don't confuse with IStorage, they are completely different
public interface ILocalStorage
{
    string StorageName { get; }

    Task<List<string>> GetAllKeysAsync();

    Task<string?> GetAsync(string key);

    Task RemoveAsync(string key);

    Task SetAsync(string key, string? value);
}
