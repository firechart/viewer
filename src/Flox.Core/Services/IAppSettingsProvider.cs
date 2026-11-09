namespace Flox.Core.Services;

public interface IAppSettingsProvider
{
    event EventHandler? SettingsChanged;

    Type SettingsType { get; }

    Task<object> GetObjectAsync();

    Task SaveObjectAsync(object value);

    // Back to the defaults of the class
    Task ResetAsync();
}

public interface IAppSettingsProvider<T> : IAppSettingsProvider where T : class, new()
{
    Task<T> GetValue();

    Task SaveValue(T value);
}
