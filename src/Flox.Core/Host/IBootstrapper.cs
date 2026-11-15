using System.Reflection;
using Flox.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Host;

/// <summary>Builds the host: holds its configuration, logging and services and runs the modules.</summary>
public interface IBootstrapper
{
    /// <summary>The application configuration.</summary>
    IConfiguration Configuration { get; }
    /// <summary>The configuration builder of the host.</summary>
    IConfigurationBuilder ConfigurationBuilder { get; }
    /// <summary>The logging builder of the host.</summary>
    ILoggingBuilder LoggingBulder { get; }
    /// <summary>The added modules in the order they run.</summary>
    IReadOnlyList<BootstrapModule> Modules { get; }
    /// <summary>The runtime description of the host.</summary>
    IFloxRuntime Runtime { get; }
    /// <summary>The service collection of the host.</summary>
    IServiceCollection Services { get; }
    /// <summary>The current logger.</summary>
    ILogger Logger { get; }
    /// <summary>Replaces the logger.</summary>
    /// <param name="logger">The new logger.</param>
    void SetLogger(ILogger logger);
    /// <summary>Starts the modules once the host is built.</summary>
    /// <param name="serviceProvider">The service provider of the built host.</param>
    void Run(IServiceProvider serviceProvider);
    /// <summary>The assemblies that reference Flox.Core, directly or not.</summary>
    IReadOnlyList<Assembly> FloxAssemblies { get; }
    /// <summary>The base address of the host, if it has one.</summary>
    string? BaseAddress { get; }

    /// <summary>The name of the hosting environment. Defaults to the process environment variable, which suits Server, MAUI and tests; a browser host has none and passes the value it receives from ASP.NET Core.</summary>
    string Environment { get; }
}
