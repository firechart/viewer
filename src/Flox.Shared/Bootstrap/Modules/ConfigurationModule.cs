using Microsoft.Extensions.Configuration;
using Serilog;

namespace Flox.Shared.Bootstrap.Modules;

public class ConfigurationModule : BootstrapModule
{
    public override void ConfigureServices()
    {
        var env = Bootstrapper.Environment;

        try
        {
            // No FloxPlatform.Server branch here, deliberately: the server no longer goes through
            // FloxBootstrapper/BootstrapModule at all (Program.cs calls WebApplication.CreateBuilder
            // (args) directly, which already loads appsettings.json/appsettings.{env}.json, User
            // Secrets in Development and environment variables, in the correct precedence order -
            // see the server host). Historically, when the server DID run
            // this module, re-adding the json files as later (= higher-priority) sources here shadowed
            // User Secrets and the environment variables CreateBuilder already read with whatever was
            // still in appsettings.{env}.json for the same key - a config value from a file silently
            // beat a value meant to override it. Keep that in mind if a server host is ever wired
            // through this bootstrapper chain again.
            if (Runtime.Platform == FloxPlatform.Console)
            {
                var basePath = EnvironmentUtil.GetBaseDirectory();
                if (!basePath.EndsWith(Path.DirectorySeparatorChar))
                {
                    basePath += Path.DirectorySeparatorChar;
                }

                ConfigurationBuilder.AddJsonFile($"{basePath}appsettings.json", false, true);
                ConfigurationBuilder.AddJsonFile($"{basePath}appsettings.{env}.json", true, true);
            }

            // WASM: AddJsonFile works here because WebAssemblyHostBuilder.Configuration wires an
            // HTTP-based file provider under the hood (fetches from wwwroot), not a physical one.
            // MAUI does NOT get this branch - see MauiConfigurationModule (the MAUI client library),
            // which FloxMauiBootstrapper always swaps in via ReplaceModule instead: no MAUI target
            // reliably exposes appsettings.*.json as a loose physical file next to the executable
            // (confirmed: this exact FileNotFoundException on a real Windows run), so MAUI reads
            // them as compiled-in EmbeddedResource content instead.
            if (Runtime.Platform == FloxPlatform.Wasm)
            {
                // The env-specific file must be optional: a browser has no OS environment
                // variables, so Constants.ENVIRONMENT always falls back to "Development" here
                // regardless of the actual deployment - requiring appsettings.Development.json to
                // exist made every WASM production build crash on startup the moment that file
                // wasn't published (confirmed via a real deploy).
                ConfigurationBuilder.AddJsonFile($"appsettings.json", false, true);
                ConfigurationBuilder.AddJsonFile($"appsettings.{env}.json", true, true);
            }

            if (Runtime.Platform == FloxPlatform.Test)
            {
                ConfigurationBuilder.AddJsonFile($"appsettings.json", false, true);
            }

            // Environment variables (max priority)
            ConfigurationBuilder.AddEnvironmentVariables();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load configuration: {Message}", ex.Message);
            throw;
        }
    }
}
