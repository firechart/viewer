namespace Flox.Core.Services;

[Flags]
public enum FloxPlatform
{
    Wasm = 1,

    Maui = 2,

    Server = 4,

    Console = 8,

    Test = 16,

    All = Wasm | Maui | Server | Console | Test
}

[Flags]
public enum FloxEnvironment
{
    Development = 1,
    Test = 2,
    Production = 4,
    Staging = 8,
    All = Development | Test | Production | Staging
}

[Flags]
public enum FloxEdition
{
    Lite = 0,
    Pro = 1,
    Enterprise = 2
}

public interface IFloxRuntime
{
    FloxPlatform Platform { get; }
    FloxEdition Edition { get; set; }

    bool IsDevelopment { get; }

    bool IsProduction { get; }

    bool IsStaging { get; }

    bool IsTest { get; }
    string EnvironmentName { get; }
    string BaseAddress { get; }

    bool Offline { get; set; }
}

public abstract class FloxRuntime : IFloxRuntime
{
    public abstract FloxPlatform Platform { get; }

    public virtual FloxEdition Edition { get; set; }

    public virtual string? StartupPath
    {
        get
        {
            if (string.IsNullOrEmpty(Environment.ProcessPath))
            {
                return null;
            }

            var fi = new FileInfo(Environment.ProcessPath);
            if (!fi.Exists)
            {
                return null;
            }

            return fi.DirectoryName;
        }
    }

    public bool IsDevelopment
    {
        get
        {
            return string.IsNullOrEmpty(EnvironmentName) || string.Equals("development", EnvironmentName, StringComparison.OrdinalIgnoreCase) || string.Equals("demo", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsProduction
    {
        get
        {
            return string.Equals("production", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsStaging
    {
        get
        {
            return string.Equals("staging", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsTest
    {
        get
        {
            return string.Equals("test", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public abstract string EnvironmentName { get; }
    public bool Offline { get; set; }
    public string BaseAddress { get; set; } = default!;
}
