using Flox.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Host;

/// <summary>A unit of startup configuration of a host: registers services when the bootstrapper is configured and may run code once the host is built.</summary>
public abstract class BootstrapModule
{
    /// <summary>The application configuration, read from <see cref="ConfigurationBuilder"/>.</summary>
    public IConfiguration Configuration
    {
        get
        {
            return (IConfiguration)ConfigurationBuilder;
        }
    }

    /// <summary>The configuration builder of the host.</summary>
    public IConfigurationBuilder ConfigurationBuilder
    {
        get
        {
            return Bootstrapper.ConfigurationBuilder;
        }
    }

    /// <summary>The logging builder of the host.</summary>
    public ILoggingBuilder LoggingBulder
    {
        get
        {
            return Bootstrapper.LoggingBulder;
        }
    }

    /// <summary>The service collection of the host.</summary>
    public IServiceCollection Services
    {
        get
        {
            return Bootstrapper.Services;
        }
    }

    /// <summary>The runtime description of the host (platform, edition, environment).</summary>
    public IFloxRuntime Runtime
    {
        get
        {
            return Bootstrapper.Runtime;
        }
    }

    /// <summary>The logger of the host.</summary>
    public ILogger Logger
    {
        get
        {
            return Bootstrapper.Logger;
        }
    }

    /// <summary>The bootstrapper that owns this module; set when the module is added.</summary>
    public IBootstrapper Bootstrapper { get; internal set; } = null!;
    /// <summary>Registers the services of the module in <see cref="Services"/>.</summary>
    public abstract void ConfigureServices();

    /// <summary>Runs after the host is built; does nothing by default.</summary>
    /// <param name="serviceProvider">The service provider of the built host.</param>
    /// <returns>A task that completes when the module has finished.</returns>
    public virtual Task RunAsync(IServiceProvider serviceProvider)
    {
        return Task.CompletedTask;
    }
}
