using Flox.Shared.Logging;
using Flox.Shared.Services;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Flox.Tests.Platform;

[TestFixture]
public class FloxLogLevelsTests
{
    private sealed class Capture : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values)
                                         .Build();
    }

    private readonly record struct BuiltLogger(Serilog.ILogger Logger, Capture Sink);

    private static BuiltLogger Build(FloxLogLevels levels)
    {
        var sink = new Capture();
        var configuration = new LoggerConfiguration().WriteTo.Sink(sink);
        levels.Configure(configuration);
        return new BuiltLogger(configuration.CreateLogger(), sink);
    }

    private void TheInitialLevelsComeFromAppsettings()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:Microsoft"] = "Error"
        }));

        Assert.That(levels.Initial.Default, Is.EqualTo("Warning"));
        Assert.That(levels.Initial.Overrides["Microsoft"], Is.EqualTo("Error"));
        Assert.That(levels.Initial.Overrides["Npgsql"], Is.Null, "a namespace that appsettings does not mention follows the default");
        Assert.That(levels.Current, Is.EqualTo(levels.Initial).Using<LogLevelSettings>(SameLevels));
        Assert.That(levels.IsInitial(), Is.True);
    }

    private void WithoutAppsettingsTheDefaultIsTheOneGivenOrInformation()
    {
        Assert.That(FloxLogLevels.FromConfiguration(Configuration([])).Initial.Default, Is.EqualTo("Information"));
        Assert.That(FloxLogLevels.FromConfiguration(Configuration([]), LogEventLevel.Debug).Initial.Default, Is.EqualTo("Debug"));
        Assert.That(FloxLogLevels.FromConfiguration(Configuration(new Dictionary<string, string?> { ["Serilog:MinimumLevel"] = "Error" }), LogEventLevel.Debug).Initial.Default, Is.EqualTo("Error"), "a level written directly under MinimumLevel");
    }

    private void ChangingTheLevelsActsOnARunningLogger()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration([]));
        var (logger, sink) = Build(levels);
        var microsoft = logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing");
        var own = logger.ForContext(Constants.SourceContextPropertyName, "FireChart.Viewer.Thing");

        own.Debug("own, before");
        Assert.That(sink.Events, Is.Empty, "Information is the default");

        levels.Apply(new LogLevelSettings("Debug", new Dictionary<string, string?>()));
        own.Debug("own, after");
        microsoft.Debug("framework, after");
        Assert.That(sink.Events.Select(row => row.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "own, after", "framework, after" }), "the default reaches every namespace that has no level of its own");

        levels.Apply(new LogLevelSettings("Debug", new Dictionary<string, string?> { ["Microsoft"] = "Warning" }));
        sink.Events.Clear();
        microsoft.Information("framework, quiet");
        microsoft.Warning("framework, loud");
        own.Debug("own, still");
        Assert.That(sink.Events.Select(row => row.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "framework, loud", "own, still" }), "a namespace with a level of its own is held to it");
    }

    private void ResetReturnsToAppsettings()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration(new Dictionary<string, string?> { ["Serilog:MinimumLevel:Default"] = "Warning" }));

        levels.Apply(new LogLevelSettings("Verbose", new Dictionary<string, string?> { ["Npgsql"] = "Debug" }));
        Assert.That(levels.IsInitial(), Is.False);

        levels.Reset();

        Assert.That(levels.IsInitial(), Is.True);
        Assert.That(levels.Current.Default, Is.EqualTo("Warning"));
    }

    private void AnUnknownLevelIsRefusedAndChangesNothing()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration([]));

        Assert.Throws<ArgumentException>(() => levels.Apply(new LogLevelSettings("Loud", new Dictionary<string, string?>())));
        Assert.Throws<ArgumentException>(() => levels.Apply(new LogLevelSettings("Debug", new Dictionary<string, string?> { ["Npgsql"] = "Chatty" })));

        Assert.That(levels.IsInitial(), Is.True);
        Assert.That(levels.Current.Default, Is.EqualTo("Information"));
    }

    private void AMessageOfThePushChannelThatCarriesLevelsIsApplied()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration([]));

        var applied = levels.TryApply(new EventMessage(new LogLevelSettings("Error", new Dictionary<string, string?> { ["Flox"] = "Debug" })));

        Assert.That(applied, Is.True);
        Assert.That(levels.Current.Default, Is.EqualTo("Error"));
        Assert.That(levels.Current.Overrides["Flox"], Is.EqualTo("Debug"));
        Assert.That(levels.Current.Overrides["Npgsql"], Is.Null, "a namespace that is not named follows the default");
    }

    private void AMessageOfAnotherKindIsLeftAlone()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration([]));

        var applied = levels.TryApply(new EventMessage(new { Something = "else" }));

        Assert.That(applied, Is.False);
        Assert.That(levels.IsInitial(), Is.True);
    }

    private void ANamespaceWithoutALevelFollowsTheNearestOneAboveIt()
    {
        var levels = FloxLogLevels.FromConfiguration(Configuration([]));

        levels.Apply(new LogLevelSettings("Debug", new Dictionary<string, string?> { ["Microsoft"] = "Warning" }));

        var (logger, sink) = Build(levels);
        logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting").Information("quiet");
        logger.ForContext(Constants.SourceContextPropertyName, "System.Net").Debug("loud");
        Assert.That(sink.Events.Select(row => row.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(new[] { "loud" }), "Microsoft.AspNetCore follows Microsoft, System follows the default");
    }

    // Records compare their dictionaries by reference: the levels are compared by value
    private static bool SameLevels(LogLevelSettings left, LogLevelSettings right)
    {
        return left.Default == right.Default && left.Overrides.Count == right.Overrides.Count && left.Overrides.All(row => right.Overrides.TryGetValue(row.Key, out var value) && value == row.Value);
    }

    [Test]
    public void FloxLogLevelsScenario()
    {
        Assert.Multiple(() =>
        {
            TheInitialLevelsComeFromAppsettings();
            WithoutAppsettingsTheDefaultIsTheOneGivenOrInformation();
            ChangingTheLevelsActsOnARunningLogger();
            ResetReturnsToAppsettings();
            AnUnknownLevelIsRefusedAndChangesNothing();
            AMessageOfThePushChannelThatCarriesLevelsIsApplied();
            AMessageOfAnotherKindIsLeftAlone();
            ANamespaceWithoutALevelFollowsTheNearestOneAboveIt();
        });
    }
}
