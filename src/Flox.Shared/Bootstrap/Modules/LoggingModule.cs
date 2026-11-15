using System.Globalization;
using Flox.Shared.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Settings.Configuration;

namespace Flox.Shared.Bootstrap.Modules;

/// <summary>
/// Configures Serilog logging: console in a regular host, browser console in WebAssembly, with levels that can be changed at run time.
/// </summary>
public class LoggingModule : BootstrapModule
{
    /// <summary>
    /// Output template of the browser console, with milliseconds in the timestamp.
    /// </summary>
    public const string BrowserOutputTemplate = "[{Level:u3}] [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Output template of the console and the debug output.
    /// </summary>
    public const string OutputTemplate = "[{Level:u3}] [{Timestamp:HH:mm:ss}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Level from which the console writes to stderr; set on Apple platforms, where the debugger shows stderr only.
    /// </summary>
    public static readonly LogEventLevel? StandardErrorFromLevel = OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? LogEventLevel.Verbose : null;

    /// <inheritdoc />
    public override void ConfigureServices()
    {
        const string template = OutputTemplate;

        // The assemblies are named: in a trimmed browser build the reader finds none by itself and fails
        var loggerConfiguration = new LoggerConfiguration().ReadFrom
                                                           .Configuration(Configuration, new ConfigurationReaderOptions(typeof(LoggerConfiguration).Assembly))
                                                           .Enrich
                                                           .FromLogContext();

        // The levels are switches: the server changes them while the client runs (FloxLogLevels, the push channel). They start from the
        // configuration (in the browser /appsettings.client.json brings the levels of the server: every Debug line of the framework is a
        // call into JS); a debug build outside the browser starts at Debug unless the configuration says otherwise
        var levels = FloxLogLevels.FromConfiguration(Configuration,
#if DEBUG
                                                     Runtime.Platform != FloxPlatform.Wasm && !OperatingSystem.IsBrowser() ? LogEventLevel.Debug : null
#else
                                                     null
#endif
        );
        levels.Configure(loggerConfiguration);
        Services.AddSingleton(levels);

        if (Runtime.Platform == FloxPlatform.Wasm || OperatingSystem.IsBrowser())
        {
            loggerConfiguration = loggerConfiguration.WriteTo.BrowserConsole(outputTemplate: BrowserOutputTemplate, formatProvider: CultureInfo.CurrentCulture);
        }
        else
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console(outputTemplate: template, formatProvider: CultureInfo.CurrentCulture, standardErrorFromLevel: StandardErrorFromLevel);
#if DEBUG
            loggerConfiguration = loggerConfiguration.WriteTo.Debug(outputTemplate: template, formatProvider: CultureInfo.CurrentCulture);
#endif
        }

        ConfigureLoggingConfiguration(ref loggerConfiguration);

        var logger = loggerConfiguration.CreateLogger();
        Bootstrapper.SetLogger(new SerilogLoggerFactory(logger, false).CreateLogger("Bootstrap"));
        LoggingBulder.ClearProviders();
        LoggingBulder.AddSerilog(logger);
    }

    /// <summary>
    /// Hook for a derived module to add sinks or enrichers before the logger is created.
    /// </summary>
    protected virtual void ConfigureLoggingConfiguration(ref LoggerConfiguration loggerConfiguration)
    {
    }
}
