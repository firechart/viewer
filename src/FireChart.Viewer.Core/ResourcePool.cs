using Hl7.Fhir.ElementModel;

namespace FireChart.Viewer.Core;

/// <summary>
/// A resource as the server sent it and where it came from.
/// </summary>
/// <param name="Json">The resource as JSON text.</param>
/// <param name="FullUrl">The <c>fullUrl</c> of the entry of the Bundle, or <see langword="null"/>.</param>
/// <example>
/// <code>
/// var entry = new PoolEntry(encounterJson, "https://fhir.example.org/r4/Encounter/e1");
/// </code>
/// </example>
public sealed record PoolEntry(string Json, string? FullUrl = null);

/// <summary>
/// The resources a rendering can reach besides the one it renders: whatever came in the Bundle with it (included, from <c>$everything</c>, from other searches).
/// </summary>
/// <remarks>
/// A template follows a Reference with <c>{{ reference | resolve }}</c>; the renderer looks it up here and does not fetch anything itself. The references the
/// templates looked for and did not find are kept in <see cref="Misses"/>, so the caller can fetch them in a second request and render again.
/// </remarks>
/// <example>
/// <code>
/// var pool = FhirRenderer.CreatePool(bundle.Entries.Select(e =&gt; new PoolEntry(e.Json, e.FullUrl)));
/// var row = await renderer.RenderTileAsync(observationJson, pool: pool);
/// if (pool.Misses.Count &gt; 0) { /* fetch them, pool.AddAll(...), render again */ }
/// </code>
/// </example>
public sealed class ResourcePool
{
    private const string History = "_history";

    private readonly Dictionary<string, ITypedElement> _byKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _misses = new(StringComparer.Ordinal);
    private readonly Dictionary<ITypedElement, FhirNode> _nodes = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The number of resources in the pool.
    /// </summary>
    public int Count => _byKey.Count;

    /// <summary>
    /// What a terminology server said about the codes of the records; <see langword="null"/> when no server is asked.
    /// </summary>
    public TerminologyMap? Terminology { get; set; }

    /// <summary>
    /// <c>Type/id</c> of the relative references the templates looked for and did not find: what a second request may fetch.
    /// </summary>
    public IReadOnlyCollection<string> Misses => _misses;

    /// <summary>
    /// <c>Type/id</c> of the references whose resources the server says do not exist; a template asks with the filter <c>missing</c>.
    /// </summary>
    public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds resources as the server sent them, read with the model of their release; a resource that cannot be read is skipped.
    /// </summary>
    /// <param name="entries">The resources and their <c>fullUrl</c>.</param>
    /// <param name="model">The model of the release of the resources; R4 when <see langword="null"/>.</param>
    public void AddAll(IEnumerable<PoolEntry> entries, Hl7.Fhir.Introspection.ModelInspector? model = null)
    {
        FhirRenderer.AddEntries(this, entries, model);
    }

    /// <summary>
    /// Records a relative reference that was looked for and not found; a reference to another server (an absolute URL) or without an id is not recorded.
    /// </summary>
    /// <param name="reference">The reference as written in the record (<c>Patient/p1</c>).</param>
    public void Missed(string? reference)
    {
        if (reference is not null && !reference.Contains("://", StringComparison.Ordinal) && KeyOf(reference) is { } key && !_byKey.ContainsKey(key))
        {
            _misses.Add(key);
            MissCount++;
        }
    }

    // How many times a template looked for a resource that a second request may fetch: a row whose rendering raised it is rendered again after the fetch
    internal int MissCount { get; private set; }

    /// <summary>
    /// Adds a resource that is already read; a resource without an id is reachable by its <paramref name="fullUrl"/> only.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="fullUrl">The <c>fullUrl</c> of its entry, or <see langword="null"/>.</param>
    public void Add(ITypedElement resource, string? fullUrl = null)
    {
        var type = resource.InstanceType ?? resource.Name;
        if (resource.Children("id").FirstOrDefault()?.Value is string id && id.Length > 0)
        {
            _byKey.TryAdd($"{type}/{id}", resource);
        }

        if (!string.IsNullOrEmpty(fullUrl))
        {
            _byKey.TryAdd(fullUrl, resource);
        }
    }

    // The node of a resource of the pool: the same one every time, with the children it has read
    internal FhirNode NodeOf(ITypedElement resource)
    {
        if (!_nodes.TryGetValue(resource, out var node))
        {
            node = new FhirNode(resource);
            _nodes[resource] = node;
        }

        return node;
    }

    /// <summary>
    /// Finds the resource of a reference.
    /// </summary>
    /// <param name="reference"><c>Patient/1</c>, or an absolute URL (the <c>fullUrl</c>, or the address of the resource on the server), with or without a version.</param>
    /// <returns>The resource, or <see langword="null"/> when the pool does not have it.</returns>
    public ITypedElement? Find(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.StartsWith('#'))
        {
            return null;
        }

        if (_byKey.TryGetValue(reference, out var found))
        {
            return found;
        }

        return KeyOf(reference) is { } key && _byKey.TryGetValue(key, out var byKey) ? byKey : null;
    }

    /// <summary>
    /// The <c>Type/id</c> of a reference, however it is written.
    /// </summary>
    /// <param name="reference">A relative or absolute reference, with or without <c>_history</c>.</param>
    /// <returns><c>Type/id</c>, or <see langword="null"/> when the reference has none (a <c>urn:</c>, a fragment <c>#c1</c>).</returns>
    /// <example>
    /// <code>
    /// ResourcePool.KeyOf("https://fhir.example.org/r4/Patient/p1/_history/3");   // "Patient/p1"
    /// </code>
    /// </example>
    public static string? KeyOf(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.StartsWith('#'))
        {
            return null;
        }

        var segments = reference.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var end = Array.IndexOf(segments, History);
        if (end < 0)
        {
            end = segments.Length;
        }

        return end >= 2 ? $"{segments[end - 2]}/{segments[end - 1]}" : null;
    }
}
