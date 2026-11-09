using Flox.Shared.Services;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Flox.Shared.Logging;

/// <summary>
///     The levels of the Serilog logger, changeable while the application runs. The logger is built with one switch for the default
///     level and one for each namespace prefix of <see cref="Prefixes" />; changing a switch takes effect at once, in every thread.
///     What appsettings says (Serilog:MinimumLevel) is the initial state; <see cref="Apply" /> replaces it, <see cref="Reset" /> brings it back.
///     A namespace without a level of its own follows the nearest namespace above it that has one, else the default: its switch is kept equal to that.
/// </summary>
public sealed class FloxLogLevels
{
    // Namespaces that can be given a level even when appsettings does not mention them: the loggers that are the loudest
    public static readonly IReadOnlyList<string> WellKnownPrefixes = ["Microsoft", "Microsoft.AspNetCore", "System", "OrchardCore", "Npgsql", "Marten", "Flox", "FireChart"];

    public static readonly IReadOnlyList<string> LevelNames = Enum.GetNames<LogEventLevel>();

    private readonly object _gate = new();
    private readonly LoggingLevelSwitch _default = new();
    private readonly Dictionary<string, LoggingLevelSwitch> _switches = new(StringComparer.Ordinal);
    private readonly List<string> _prefixes;
    private LogLevelSettings _current;

    private FloxLogLevels(LogLevelSettings initial)
    {
        Initial = initial;
        _prefixes = initial.Overrides.Keys.ToList();
        foreach (var prefix in _prefixes)
        {
            _switches[prefix] = new LoggingLevelSwitch();
        }

        _current = initial;
        Apply(initial);
    }

    // What the configuration says; the state that Reset returns to
    public LogLevelSettings Initial { get; }

    public LogLevelSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    // The namespaces that have a switch, in the order of the list
    public IReadOnlyList<string> Prefixes
    {
        get
        {
            return _prefixes;
        }
    }

    // The default level comes from Serilog:MinimumLevel (a level, or the object with Default and Override), else from defaultLevel, else Information
    public static FloxLogLevels FromConfiguration(IConfiguration configuration, LogEventLevel? defaultLevel = null)
    {
        var section = configuration.GetSection("Serilog:MinimumLevel");
        var configured = section.Value ?? section["Default"];
        var level = TryParseLevel(configured, out var parsed) ? parsed : defaultLevel ?? LogEventLevel.Information;

        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Override").GetChildren())
        {
            if (TryParseLevel(child.Value, out var overridden))
            {
                overrides[child.Key] = overridden.ToString();
            }
        }

        foreach (var prefix in WellKnownPrefixes)
        {
            overrides.TryAdd(prefix, null);
        }

        return new FloxLogLevels(new LogLevelSettings(level.ToString(), overrides));
    }

    public static bool TryParseLevel(string? name, out LogEventLevel level)
    {
        return Enum.TryParse(name, true, out level) && Enum.IsDefined(level);
    }

    // The default level and the level of every namespace that has a switch; whoever has none follows the default
    public void Configure(LoggerConfiguration configuration)
    {
        configuration.MinimumLevel.ControlledBy(_default);
        foreach (var prefix in _prefixes)
        {
            configuration.MinimumLevel.Override(prefix, _switches[prefix]);
        }
    }

    // Takes the levels over. A namespace that has no switch is ignored (the logger knows its namespaces from the start); a level that is
    // not one of the Serilog names is an error and changes nothing
    public void Apply(LogLevelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!TryParseLevel(settings.Default, out var defaultLevel))
        {
            throw new ArgumentException($"Unknown level '{settings.Default}'.", nameof(settings));
        }

        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var prefix in _prefixes)
        {
            settings.Overrides.TryGetValue(prefix, out var name);
            if (string.IsNullOrWhiteSpace(name))
            {
                overrides[prefix] = null;
            }
            else if (TryParseLevel(name, out var level))
            {
                overrides[prefix] = level.ToString();
            }
            else
            {
                throw new ArgumentException($"Unknown level '{name}' of '{prefix}'.", nameof(settings));
            }
        }

        lock (_gate)
        {
            _default.MinimumLevel = defaultLevel;
            foreach (var (prefix, name) in overrides)
            {
                _switches[prefix].MinimumLevel = name is null ? Inherited(prefix, overrides, defaultLevel) : Enum.Parse<LogEventLevel>(name);
            }

            _current = new LogLevelSettings(defaultLevel.ToString(), overrides);
        }
    }

    // A namespace without a level of its own follows the nearest namespace above it that has one (Microsoft.AspNetCore follows Microsoft), else the default
    private LogEventLevel Inherited(string prefix, Dictionary<string, string?> overrides, LogEventLevel defaultLevel)
    {
        var parent = _prefixes.Where(other => overrides[other] is not null && prefix.StartsWith(other + ".", StringComparison.Ordinal))
                              .OrderByDescending(other => other.Length)
                              .FirstOrDefault();
        return parent is null ? defaultLevel : Enum.Parse<LogEventLevel>(overrides[parent]!);
    }

    public void Reset()
    {
        Apply(Initial);
    }

    // True for a message of the push channel that carries levels (and then they are applied)
    public bool TryApply(EventMessage message)
    {
        if (message.PayloadType != typeof(LogLevelSettings).FullName || message.Deserialize<LogLevelSettings>() is not { } settings)
        {
            return false;
        }

        Apply(settings);
        return true;
    }

    public bool IsInitial()
    {
        var current = Current;
        return current.Default == Initial.Default && Prefixes.All(prefix => current.Overrides.GetValueOrDefault(prefix) == Initial.Overrides.GetValueOrDefault(prefix));
    }
}
