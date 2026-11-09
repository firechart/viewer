extern alias R4B;
extern alias R5;
extern alias R6;
extern alias STU3;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Flox.Core.Host;
using Hl7.Fhir.Introspection;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// Brings the assemblies of a release of the Firely SDK to the process: in the browser they are loaded on demand, elsewhere they are there already.
/// </summary>
/// <example>
/// <code>
/// // Blazor WebAssembly, after the services of the Viewer:
/// builder.Services.AddSingleton&lt;IReleaseAssemblyLoader, MyLazyReleaseLoader&gt;();
/// </code>
/// </example>
public interface IReleaseAssemblyLoader
{
    /// <summary>
    /// Makes sure the assemblies of a release are loaded.
    /// </summary>
    /// <param name="release">The release (<c>STU3</c>, <c>R4B</c>, <c>R5</c>, <c>R6</c>).</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when they are.</returns>
    Task EnsureAsync(string release, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IReleaseAssemblyLoader"/> of a process that has every assembly already (server, MAUI, tests).
/// </summary>
[FloxService(typeof(IReleaseAssemblyLoader), ServiceLifetime.Singleton)]
public sealed class NoReleaseAssemblyLoader : IReleaseAssemblyLoader
{
    /// <inheritdoc/>
    public Task EnsureAsync(string release, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// The model of a release of FHIR: what tells a template that an element repeats, what a choice type is called.
/// </summary>
/// <example>
/// <code>
/// var model = await models.GetAsync(types.Release);
/// var html = await renderer.RenderAsync(json, "view", model);
/// </code>
/// </example>
public interface IReleaseModels
{
    /// <summary>
    /// The model of a release.
    /// </summary>
    /// <param name="release">The name as <see cref="ServerResourceTypes.Release"/> has it (<c>STU3</c>, <c>R4</c>, <c>R4B</c>, <c>R5</c>, <c>R6</c>); an
    /// empty or unknown name is R4.</param>
    /// <param name="cancellationToken">Cancels the loading of the assemblies of the release.</param>
    /// <returns>The model.</returns>
    Task<ModelInspector> GetAsync(string? release, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IReleaseModels"/> of the five releases the Viewer carries; a singleton.
/// </summary>
/// <param name="loader">Loads the assemblies of a release before its model is made.</param>
/// <example>
/// <code>
/// var models = new ReleaseModels(new NoReleaseAssemblyLoader());
/// var r5 = await models.GetAsync("R5");
/// </code>
/// </example>
[FloxService(typeof(IReleaseModels), ServiceLifetime.Singleton)]
public sealed class ReleaseModels(IReleaseAssemblyLoader loader) : IReleaseModels
{
    /// <summary>
    /// The release of an empty or unknown name: <c>R4</c>.
    /// </summary>
    public const string Default = "R4";

    private readonly ConcurrentDictionary<string, ModelInspector> _loaded = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<ModelInspector> GetAsync(string? release, CancellationToken cancellationToken = default)
    {
        var name = Normalize(release);
        if (name == Default)
        {
            return Hl7.Fhir.Model.ModelInfo.ModelInspector;
        }

        if (_loaded.TryGetValue(name, out var known))
        {
            return known;
        }

        await loader.EnsureAsync(name, cancellationToken);
        return _loaded[name] = Inspector(name);
    }

    /// <summary>
    /// The name of a release as the models know it.
    /// </summary>
    /// <param name="release">A name in any case (<c>r5</c>).</param>
    /// <returns><c>STU3</c>, <c>R4B</c>, <c>R5</c>, <c>R6</c>, else <see cref="Default"/>.</returns>
    /// <example>
    /// <code>
    /// var name = ReleaseModels.Normalize("r4b"); // "R4B"
    /// </code>
    /// </example>
    public static string Normalize(string? release)
    {
        return release?.Trim().ToUpperInvariant() switch
        {
            "STU3" => "STU3",
            "R4B" => "R4B",
            "R5" => "R5",
            "R6" => "R6",
            _ => Default
        };
    }

    // A method of its own for each release: the assembly of a release is needed when its method is first run, which is after it has been loaded
    private static ModelInspector Inspector(string name)
    {
        return name switch
        {
            "STU3" => Stu3(),
            "R4B" => R4b(),
            "R5" => R5(),
            _ => R6()
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModelInspector Stu3()
    {
        return STU3::Hl7.Fhir.Model.ModelInfo.ModelInspector;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModelInspector R4b()
    {
        return R4B::Hl7.Fhir.Model.ModelInfo.ModelInspector;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModelInspector R5()
    {
        return R5::Hl7.Fhir.Model.ModelInfo.ModelInspector;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModelInspector R6()
    {
        return R6::Hl7.Fhir.Model.ModelInfo.ModelInspector;
    }
}
