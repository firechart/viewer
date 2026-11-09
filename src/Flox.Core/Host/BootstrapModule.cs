using Flox.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Host;

public abstract class BootstrapModule
{
    public IConfiguration Configuration
    {
        get
        {
            return (IConfiguration)ConfigurationBuilder;
        }
    }

    public IConfigurationBuilder ConfigurationBuilder
    {
        get
        {
            return Bootstrapper.ConfigurationBuilder;
        }
    }

    public ILoggingBuilder LoggingBulder
    {
        get
        {
            return Bootstrapper.LoggingBulder;
        }
    }

    public IServiceCollection Services
    {
        get
        {
            return Bootstrapper.Services;
        }
    }

    public IFloxRuntime Runtime
    {
        get
        {
            return Bootstrapper.Runtime;
        }
    }

    public ILogger Logger
    {
        get
        {
            return Bootstrapper.Logger;
        }
    }

    public IBootstrapper Bootstrapper { get; internal set; } = null!;
    public abstract void ConfigureServices();

    public virtual Task RunAsync(IServiceProvider serviceProvider)
    {
        return Task.CompletedTask;
    }
}
