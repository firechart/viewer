namespace Flox.Core.Services;

/// <summary>The kinds of host the code runs in.</summary>
[Flags]
public enum FloxPlatform
{
    /// <summary>The browser, through WebAssembly.</summary>
    Wasm = 1,

    /// <summary>The ASP.NET Core server.</summary>
    Server = 4,

    /// <summary>A console tool.</summary>
    Console = 8,

    /// <summary>A test host.</summary>
    Test = 16,

    /// <summary>Every platform.</summary>
    All = Wasm | Server | Console | Test
}

/// <summary>The deployment environments.</summary>
[Flags]
public enum FloxEnvironment
{
    /// <summary>Development.</summary>
    Development = 1,
    /// <summary>Test.</summary>
    Test = 2,
    /// <summary>Production.</summary>
    Production = 4,
    /// <summary>Staging.</summary>
    Staging = 8,
    /// <summary>Every environment.</summary>
    All = Development | Test | Production | Staging
}

/// <summary>Describes the host the code runs in.</summary>
public interface IFloxRuntime
{
    /// <summary>The kind of host.</summary>
    FloxPlatform Platform { get; }
    /// <summary>Whether the application has a server of its own (sign-in, OData, access lists); false for a client without one (FireChart
    /// Lite, the Viewer app). What the application may do by its license is a question of the license, not of the host.</summary>
    bool HasServer { get; }

    /// <summary>Whether the environment is development.</summary>
    bool IsDevelopment { get; }

    /// <summary>Whether the environment is production.</summary>
    bool IsProduction { get; }

    /// <summary>Whether the environment is staging.</summary>
    bool IsStaging { get; }

    /// <summary>Whether the environment is test.</summary>
    bool IsTest { get; }
    /// <summary>The name of the hosting environment.</summary>
    string EnvironmentName { get; }
    /// <summary>The base address of the host.</summary>
    string BaseAddress { get; }

    /// <summary>Whether the application works without a connection.</summary>
    bool Offline { get; set; }
}

/// <summary>Base class of the runtime descriptions of the hosts.</summary>
public abstract class FloxRuntime : IFloxRuntime
{
    /// <summary>The kind of host.</summary>
    public abstract FloxPlatform Platform { get; }

    /// <summary>Whether the application has a server of its own; true unless a host says otherwise.</summary>
    public virtual bool HasServer
    {
        get
        {
            return true;
        }
    }

    /// <summary>The folder of the running executable; null when there is none.</summary>
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

    /// <summary>Whether the environment is development: the name is empty, <c>development</c> or <c>demo</c>.</summary>
    public bool IsDevelopment
    {
        get
        {
            return string.IsNullOrEmpty(EnvironmentName) || string.Equals("development", EnvironmentName, StringComparison.OrdinalIgnoreCase) || string.Equals("demo", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether the environment is production.</summary>
    public bool IsProduction
    {
        get
        {
            return string.Equals("production", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether the environment is staging.</summary>
    public bool IsStaging
    {
        get
        {
            return string.Equals("staging", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether the environment is test.</summary>
    public bool IsTest
    {
        get
        {
            return string.Equals("test", EnvironmentName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The name of the hosting environment.</summary>
    public abstract string EnvironmentName { get; }
    /// <summary>Whether the application works without a connection.</summary>
    public bool Offline { get; set; }
    /// <summary>The base address of the host.</summary>
    public string BaseAddress { get; set; } = default!;
}
