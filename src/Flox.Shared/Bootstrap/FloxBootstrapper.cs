using System.Globalization;
using Flox.Shared.Bootstrap.Modules;
using Flox.Shared.Branding;
using Flox.Shared.Host;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;

namespace Flox.Shared.Bootstrap;

public abstract class FloxBootstrapper : Bootstrapper
{
    // Mutable App property: a specialized app's own bootstrapper overrides branding by assigning
    // Branding.App in its own constructor, after base(...) runs - see FloxBrandingProvider.
    protected FloxBrandingProvider Branding { get; }

    protected FloxBootstrapper(IServiceCollection services, IConfigurationBuilder configuration, ILoggingBuilder loggingBuilder, string? baseAddress = null, string? environment = null) : base(services, configuration, loggingBuilder, baseAddress, environment)
    {
        Branding = new FloxBrandingProvider(FloxPlatformBranding.Default);
        Services.AddSingleton<IFloxBrandingProvider>(Branding);

        // Platform-default menu, same override-friendly shape as Branding: an app registers its own
        // IFloxMenuSource alongside (resolved as IEnumerable<T> by FloxShell, so this is additive,
        // not override-by-replacement like Branding.App is).
        Services.AddSingleton<IFloxMenuSource, SharedMenuSource>();

        // Factory registration, not eager: appsettings.*.json aren't merged into Configuration
        // until ConfigurationModule.ConfigureServices() runs (later, during the ConfigureServices() phase) - this
        // factory only actually executes on first resolution, well after that. Every host
        // (Server/WASM/MAUI) and every Razor component (SharedMenuSource, ProfileWidget) reads this
        // same validated instance instead of re-parsing "Flox:Oidc" config themselves.
        Services.AddSingleton(_ =>
        {
            var options = new FloxOidcOptions();
            Configuration.GetSection("Flox:Oidc")
                         .Bind(options);
            options.Validate();
            return options;
        });

        // Same pattern as FloxOidcOptions above - see FloxTelemetryOptions.
        Services.AddSingleton(_ =>
        {
            var options = new FloxTelemetryOptions();
            Configuration.GetSection("Flox:OpenTelemetry")
                         .Bind(options);
            options.Validate();
            return options;
        });

        RegisterValidators();
        AddModule(new ConfigurationModule());
        AddModule(new AuthorizationPoliciesModule());
        AddModule(new RadzenModule());
        AddModule(new ThemingModule());
    }

    protected override Microsoft.Extensions.Logging.ILogger CreateBootstrapLogger()
    {
        var isBrowser = OperatingSystem.IsBrowser();
        var serilogLogger = isBrowser
            ? new LoggerConfiguration().WriteTo
                                       .BrowserConsole(outputTemplate: LoggingModule.BrowserOutputTemplate, formatProvider: CultureInfo.CurrentCulture)
                                       .CreateLogger()
            : new LoggerConfiguration().WriteTo
                                       .Console(formatProvider: CultureInfo.CurrentCulture, standardErrorFromLevel: LoggingModule.StandardErrorFromLevel)
                                       .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger, false).CreateLogger("Bootstrap");
        if (isBrowser)
        {
            logger.LogInformation("Created bootstrap/browser logger");
        }
        else
        {
            logger.LogInformation("Created bootstrap/console logger");
        }

        return logger;
    }

    private void RegisterValidators()
    {
        Services.AddValidatorsFromAssemblies(FloxAssemblies);
    }
}
