using System.Reflection;
using Flox.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Host;

public interface IBootstrapper
{
    IConfiguration Configuration { get; }
    IConfigurationBuilder ConfigurationBuilder { get; }
    ILoggingBuilder LoggingBulder { get; }
    IReadOnlyList<BootstrapModule> Modules { get; }
    IFloxRuntime Runtime { get; }
    IServiceCollection Services { get; }
    ILogger Logger { get; }
    void SetLogger(ILogger logger);
    void Run(IServiceProvider serviceProvider);
    IReadOnlyList<Assembly> FloxAssemblies { get; }
    string? BaseAddress { get; }

    // The real ASPNETCORE_ENVIRONMENT-equivalent name for this host. Defaults to
    // Constants.ENVIRONMENT (a real env var read - correct for Server/MAUI/Test, which have a real
    // OS environment). WASM has no OS environment to read from a browser sandbox, so its
    // bootstrapper overrides this with the value ASP.NET Core's own Blazor Web App infrastructure
    // already threads through to the client (builder.HostEnvironment.Environment in Program.cs) -
    // see ConfigurationModule, which reads this instead of Constants.ENVIRONMENT directly.
    string Environment { get; }
}
