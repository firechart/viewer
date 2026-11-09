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

public class Bootstrapper : IBootstrapper
{
    private readonly List<BootstrapModule> _modules = [];
    private readonly List<Type> _modulesTypes = [];

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

    public string? BaseAddress { get; }
    public string Environment { get; }

    public IConfiguration Configuration
    {
        get
        {
            return (IConfiguration)ConfigurationBuilder;
        }
    }

    public IConfigurationBuilder ConfigurationBuilder { get; }
    public IReadOnlyList<Assembly> FloxAssemblies { get; private set; } = [];

    // Name prefixes of the assemblies that may hold [FloxService] types; in a browser only those are force-loaded
    // (the reference graph has ~190 assemblies, and each load costs a debugger round trip)
    protected virtual IReadOnlyList<string> FloxAssemblyNamePrefixes
    {
        get
        {
            return ["Flox."];
        }
    }

    public ILoggingBuilder LoggingBulder { get; }

    public ILogger Logger { get; private set; }

    public void SetLogger(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Cheap logger available immediately in the constructor, before any BootstrapModule
    // (including LoggingModule) has had a chance to configure the real one. Descendants
    // override this to plug in whatever minimal sink makes sense for their host (console,
    // browser console, etc.); LoggingModule replaces it later via SetLogger.
    protected virtual ILogger CreateBootstrapLogger()
    {
        return NullLogger.Instance;
    }

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

    public IReadOnlyList<BootstrapModule> Modules
    {
        get
        {
            return _modules;
        }
    }

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

    public void Run(IServiceProvider serviceProvider)
    {
        foreach (var module in Modules)
        {
            module.RunAsync(serviceProvider);
        }
    }
}
