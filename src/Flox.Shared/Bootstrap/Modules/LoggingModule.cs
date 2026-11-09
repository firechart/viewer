using System.Globalization;
using Flox.Shared.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Settings.Configuration;

namespace Flox.Shared.Bootstrap.Modules;

public class LoggingModule : BootstrapModule
{
    // The browser console has no time of its own per line; milliseconds make the startup timeline readable
    public const string BrowserOutputTemplate = "[{Level:u3}] [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";

    public const string OutputTemplate = "[{Level:u3}] [{Timestamp:HH:mm:ss}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}";

    // On Apple platforms the debugger and `simctl --console-pty` show stderr (NSLog) but not Console.Out
    public static readonly LogEventLevel? StandardErrorFromLevel = OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? LogEventLevel.Verbose : null;

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

    protected virtual void ConfigureLoggingConfiguration(ref LoggerConfiguration loggerConfiguration)
    {
    }
}
