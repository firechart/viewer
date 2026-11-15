using System.Reflection;
using System.Runtime.Loader;
using Flox.Core.Diagnostics;
using Flox.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flox.Core.Host;

/// <summary>Default <see cref="IBootstrapper"/>: finds the assemblies that use Flox, registers their <c>[FloxService]</c> types and runs the added modules.</summary>
public class Bootstrapper : IBootstrapper
{
    private readonly List<BootstrapModule> _modules = [];
    private readonly List<Type> _modulesTypes = [];

    /// <summary>Creates the bootstrapper and scans the loaded assemblies for services.</summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="configuration">The configuration builder of the host.</param>
    /// <param name="loggingBuilder">The logging builder of the host.</param>
    /// <param name="baseAddress">The base address of the host, if it has one.</param>
    /// <param name="environment">The environment name; the process environment variable is used when it is null.</param>
    public Bootstrapper(IServiceCollection services, IConfigurationBuilder configuration, ILoggingBuilder loggingBuilder, string? baseAddress = null, string? environment = null)
    {
        StartupTimeline.Mark("Bootstrapper: starts");

#pragma warning disable CA2214
        using (StartupTimeline.Span("Bootstrapper: bootstrap logger"))
        {
            Logger = CreateBootstrapLogger();
            Logger.LogInformation("Bootstrap logger created");
        }

        using (StartupTimeline.Span("Bootstrapper: ForceLoadAssemblies"))
        {
            ForceLoadAssemblies();
        }
#pragma warning restore CA2214

        StartupTimeline.Mark("Bootstrapper: assemblies loaded, registering the host");
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Services.AddSingleton<IBootstrapper>(this);

        ConfigurationBuilder = configuration ?? throw new ArgumentNullException(nameof(configuration));
        LoggingBulder = loggingBuilder ?? throw new ArgumentNullException(nameof(loggingBuilder));
        BaseAddress = baseAddress;
        // Constants.ENVIRONMENT reads a real OS env var - correct for Server/MAUI/Test, meaningless
        // in a browser sandbox. Callers that have a real value from elsewhere (WASM's
        // builder.HostEnvironment.Environment) pass it explicitly; everyone else keeps the old
        // env-var-based default.
        Environment = environment ?? Constants.ENVIRONMENT;

        StartupTimeline.Mark("Bootstrapper: environment resolved");
        Services.AddSingleton<IFloxShell, FloxShell>();
        StartupTimeline.Mark("Bootstrapper: host registered");

        ScanFloxAssemblies();
    }

    /// <summary>The base address of the host, if it has one.</summary>
    public string? BaseAddress { get; }
    /// <summary>The name of the hosting environment.</summary>
    public string Environment { get; }

    /// <summary>The application configuration, read from <see cref="ConfigurationBuilder"/>.</summary>
    public IConfiguration Configuration
    {
        get
        {
            return (IConfiguration)ConfigurationBuilder;
        }
    }

    /// <summary>The configuration builder of the host.</summary>
    public IConfigurationBuilder ConfigurationBuilder { get; }
    /// <summary>The assemblies that reference Flox.Core, directly or not.</summary>
    public IReadOnlyList<Assembly> FloxAssemblies { get; private set; } = [];

    /// <summary>Name prefixes of the assemblies that may hold <c>[FloxService]</c> types; in a browser only those are force-loaded, since each load costs a debugger round trip.</summary>
    protected virtual IReadOnlyList<string> FloxAssemblyNamePrefixes
    {
        get
        {
            return ["Flox."];
        }
    }

    /// <summary>The logging builder of the host.</summary>
    public ILoggingBuilder LoggingBulder { get; }

    /// <summary>The current logger; the bootstrap logger until a module replaces it.</summary>
    public ILogger Logger { get; private set; }

    /// <summary>Replaces the logger.</summary>
    /// <param name="logger">The new logger.</param>
    public void SetLogger(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Creates a cheap logger that is available in the constructor, before any module has configured the real one; <see cref="SetLogger"/> replaces it later. Hosts override it to plug in a console or browser sink.</summary>
    protected virtual ILogger CreateBootstrapLogger()
    {
        return NullLogger.Instance;
    }

    /// <summary>Loads the assemblies that may hold <c>[FloxService]</c> types, so the scan sees them: in a browser by walking the references of the entry assembly, elsewhere by loading the DLLs next to the executable.</summary>
    protected virtual void ForceLoadAssemblies()
    {
        // Downloading a .wasm file to the browser at startup does NOT add it to
        // AssemblyLoadContext.Default.Assemblies - the runtime only loads an assembly once
        // something actually needs a type from it. ScanFloxAssemblies() below only sees already-
        // loaded assemblies, so a [FloxService] living in an assembly nothing on the eager startup
        // path touches yet (e.g. a provider only referenced from
        // a lazily-rendered admin page) never gets registered - not until the page that finally
        // references it renders, by which point DI registration already happened and it's too
        // late (confirmed: exactly this gap, reproduced via a real Release publish). Walk the
        // reference graph from the entry assembly and force-load everything, same intent as the
        // directory scan below for Server/MAUI, just driven by compile-time reference metadata
        // (GetReferencedAssemblies) since there's no directory to list in a browser sandbox.
        if (OperatingSystem.IsBrowser())
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                entry.GetName()
                     .Name!
            };
            LoadReferencedAssembliesRecursive(entry, seen);
            return;
        }

        Logger.LogInformation("Starting dynamic assembly loading...");

        var loaded = AssemblyLoadContext.Default
                                        .Assemblies
                                        .Where(a => !a.IsDynamic)
                                        .Select(a => a.Location)
                                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
        {
            if (loaded.Contains(dll))
            {
                continue;
            }

            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                Logger.LogInformation("  Loaded {Assembly}", Path.GetFileName(dll));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "  Failed to load {Assembly}", Path.GetFileName(dll));
            }
        }
    }

    private bool IsFloxAssemblyName(string name)
    {
        return FloxAssemblyNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadReferencedAssembliesRecursive(Assembly assembly, HashSet<string> seen)
    {
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (reference.Name == null || !IsFloxAssemblyName(reference.Name) || !seen.Add(reference.Name))
            {
                continue;
            }

            try
            {
                var loadedAssembly = Assembly.Load(reference);
                Logger.LogInformation("  Loaded {Assembly}", reference.Name);
                LoadReferencedAssembliesRecursive(loadedAssembly, seen);
            }
            catch (Exception ex)
            {
                // BCL/runtime assemblies referenced by name but not shipped as a .wasm (e.g.
                // reference-assembly-only facades) routinely fail to load here - expected, not
                // worth a warning; Debug keeps this out of normal startup log noise.
                Logger.LogDebug(ex, "  Failed to load {Assembly}: {Message}", reference.Name, ex.Message);
            }
        }
    }

    /// <summary>The added modules in the order they run.</summary>
    public IReadOnlyList<BootstrapModule> Modules
    {
        get
        {
            return _modules;
        }
    }

    /// <summary>The runtime description of the host, resolved from the service collection on first use.</summary>
    public IFloxRuntime Runtime
    {
        get
        {
            if (field == null)
            {
                using var tmp = Services.BuildServiceProvider();
                field = tmp.GetRequiredService<IFloxRuntime>();
            }

            return field;
        }
    } = null;

    /// <summary>The service collection of the host.</summary>
    public IServiceCollection Services { get; }

    // An assembly "is a Flox assembly" if it IS Flox.Core, or transitively references it -
    // replaces the old opt-in [FloxAssembly] attribute (easy to forget on a new project; a
    // 404-on-login bug was caused by exactly this gap). A direct-reference-only check isn't enough: the C# compiler only emits an
    // AssemblyRef for an assembly a project's own code actually uses a type from, so a thin
    // pass-through host project (just Program.cs) can transitively depend on Flox.Core through
    // another library without Flox.Core ever appearing in its own metadata - confirmed via reflection before relying on this. The recursive
    // walk over GetReferencedAssemblies() handles that correctly, no deps.json/DependencyModel
    // package needed.
    private void ScanFloxAssemblies()
    {
        using var scan = StartupTimeline.Span("Bootstrapper: ScanFloxAssemblies");
        var coreAssemblyName = typeof(Bootstrapper).Assembly.GetName()
                                                   .Name!;

        List<Assembly> loaded;
        Dictionary<string, Assembly> byName;
        using (StartupTimeline.Span("Bootstrapper: list the loaded assemblies and their names"))
        {
            loaded = AssemblyLoadContext.Default
                                        .Assemblies
                                        .Where(a => !a.IsDynamic)
                                        .ToList();
            byName = loaded.GroupBy(a => a.GetName()
                                          .Name!)
                           .ToDictionary(g => g.Key, g => g.First());
        }

        var cache = new Dictionary<string, bool>();
        using (StartupTimeline.Span($"Bootstrapper: which of {loaded.Count} assemblies depend on Flox.Core"))
        {
            FloxAssemblies = loaded.Where(a => DependsOnFloxCore(a, coreAssemblyName, byName, cache))
                                   .ToList();
        }

        using var registration = StartupTimeline.Span($"Bootstrapper: register the [FloxService] types of {FloxAssemblies.Count} assemblies");
        foreach (var asm in FloxAssemblies)
        {
            Logger.LogInformation("Found Flox assembly {FloxAssembly}",
                                  asm.GetName()
                                     .Name);

            foreach (var type in asm.GetExportedTypes())
            {
                if (!type.IsAbstract)
                {
                    var serviceAttrs = type.GetCustomAttributes<FloxServiceAttribute>();
                    foreach (var serviceAttr in serviceAttrs)
                    {
                        var descriptor = new ServiceDescriptor(serviceAttr.ServiceType ?? type, type, serviceAttr.Lifetime);
                        if (serviceAttr.Multiple)
                        {
                            Services.TryAddEnumerable(descriptor);
                        }
                        else
                        {
                            Services.TryAdd(descriptor); // TODO: Add vs TryAdd
                        }

                        Logger.LogDebug("  Registered Flox service {Service}", type.FullName);
                    }
                }
            }
        }
    }

    // Memoized since the same assembly can be reached via multiple paths in a non-trivial
    // reference graph; cache[name] = false is set before recursing as cheap insurance against an
    // infinite loop if the reference graph ever had a cycle (it shouldn't - .NET assemblies can't
    // reference each other circularly - but this makes that a wrong answer, not a stack overflow).
    // internal (not private): unit-tested directly against real loaded assemblies - see
    // Flox.Core.Tests/Host/BootstrapperAssemblyDiscoveryTests.cs. Assembly isn't practically
    // mockable, so the test exercises this against assemblies already present in the test process
    // (the test assembly itself, Flox.Core, an unrelated BCL/NUnit assembly) rather than fakes.
    internal static bool DependsOnFloxCore(Assembly assembly, string coreAssemblyName, IReadOnlyDictionary<string, Assembly> byName, Dictionary<string, bool> cache)
    {
        var name = assembly.GetName()
                           .Name!;
        if (name == coreAssemblyName)
        {
            return true;
        }

        if (cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        cache[name] = false;

        var result = assembly.GetReferencedAssemblies()
                             .Any(refName => refName.Name == coreAssemblyName || (refName.Name != null && byName.TryGetValue(refName.Name, out var refAssembly) && DependsOnFloxCore(refAssembly, coreAssemblyName, byName, cache)));

        cache[name] = result;
        return result;
    }

    /// <summary>Adds a module unless a module of the same type is already added.</summary>
    /// <param name="module">The module to add.</param>
    public void AddModule(BootstrapModule module)
    {
        var moduleType = module.GetType();
        if (!_modulesTypes.Contains(moduleType))
        {
            module.Bootstrapper = this;
            _modules.Add(module);
            _modulesTypes.Add(moduleType);
        }
    }

    /// <summary>Calls <see cref="BootstrapModule.ConfigureServices"/> of every module in order.</summary>
    public virtual void Configure()
    {
        foreach (var module in Modules)
        {
            using (StartupTimeline.Span($"Module {module.GetType().Name}.ConfigureServices"))
            {
                module.ConfigureServices();
            }
        }
    }

    /// <summary>Inserts a module into the list at the position of the module of the given type.</summary>
    /// <param name="moduleType">The type of the module that marks the position.</param>
    /// <param name="module">The module to insert.</param>
    public void InsertModuleAfter(Type moduleType, BootstrapModule module)
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            if (_modules[i]
                   .GetType() == moduleType)
            {
                module.Bootstrapper = this;
                _modules.Insert(i, module);
            }
        }
    }

    /// <summary>Replaces the module of the given type, or adds the module when there is none.</summary>
    /// <param name="moduleType">The type of the module to replace.</param>
    /// <param name="module">The replacing module.</param>
    public void ReplaceModule(Type moduleType, BootstrapModule module)
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            if (_modules[i]
                   .GetType() == moduleType)
            {
                _modules[i] = module;
                module.Bootstrapper = this;
                return;
            }
        }

        module.Bootstrapper = this;
        _modules.Add(module);
    }

    /// <summary>Starts <see cref="BootstrapModule.RunAsync(IServiceProvider)"/> of every module.</summary>
    /// <param name="serviceProvider">The service provider of the built host.</param>
    public void Run(IServiceProvider serviceProvider)
    {
        foreach (var module in Modules)
        {
            module.RunAsync(serviceProvider);
        }
    }
}
