using System.Text.Json;
using System.Text.RegularExpressions;

namespace FireChart.Viewer.Core.Bundles;

// A resource of the graph: an entry of the Bundle, or the target of a reference the Bundle does not hold (InBundle false, no JSON)
internal sealed record BundleNode(string Key, string ResourceType, string? Id, string? FullUrl, string? Json, bool InBundle)
{
    // The reference of the record in the Viewer (Patient/123), when it has an id
    public string? Reference => Id is null ? null : $"{ResourceType}/{Id}";
}

// A reference from one resource to another, by the path of the element that holds it (subject, result)
internal sealed record BundleEdge(string From, string To, string Path);

// The resources of a Bundle and the references of type Reference between them, found by the member "reference" of the JSON (so not canonical)
// and resolved as the specification says (Resolving references in Bundles): an absolute
// reference by the fullUrl of an entry, a relative one against the base of the fullUrl of the entry that holds it, else by its type and id; a
// reference to a contained resource (#id) is not an edge. What a reference names and the Bundle lacks is a node of its own, not in the Bundle
internal sealed partial class BundleGraph
{
    private BundleGraph(string? type, IReadOnlyList<BundleNode> nodes, IReadOnlyList<BundleEdge> edges)
    {
        Type = type;
        Nodes = nodes;
        Edges = edges;
        EntryCount = nodes.Count(node => node.InBundle);
        foreach (var node in nodes)
        {
            _byKey.TryAdd(node.Key, node);
        }
    }

    private readonly Dictionary<string, BundleNode> _byKey = new(StringComparer.Ordinal);

    // Bundle.type: searchset, document, transaction, collection
    public string? Type { get; }

    public IReadOnlyList<BundleNode> Nodes { get; }

    public IReadOnlyList<BundleEdge> Edges { get; }

    public int EntryCount { get; }

    public BundleNode? Find(string key)
    {
        return _byKey.GetValueOrDefault(key);
    }

    [GeneratedRegex(@"^(?:(?<base>.*)/)?(?<type>[A-Z][A-Za-z]+)/(?<id>[A-Za-z0-9\-.]{1,64})(?:/_history/[^/]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RestfulReference();

    // The JSON of a Bundle; FormatException when it is not one
    public static BundleGraph Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new FormatException("The text is not well formed JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("resourceType", out var resourceType) || resourceType.GetString() != "Bundle")
            {
                throw new FormatException("The text is not a FHIR Bundle.");
            }

            var type = root.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
            var entries = new List<Entry>();
            if (root.TryGetProperty("entry", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var entry in array.EnumerateArray())
                {
                    index++;
                    if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object ||
                        !resource.TryGetProperty("resourceType", out var name) || name.GetString() is not { Length: > 0 } typeName)
                    {
                        continue;
                    }

                    var id = resource.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
                    var fullUrl = entry.TryGetProperty("fullUrl", out var url) && url.ValueKind == JsonValueKind.String && url.GetString() is { Length: > 0 } address ? address : null;
                    var key = fullUrl ?? (id is null ? $"#{index}" : $"{typeName}/{id}");
                    entries.Add(new Entry(new BundleNode(key, typeName, id, fullUrl, resource.GetRawText(), true), resource));
                }
            }

            return Build(type, entries);
        }
    }

    private sealed record Entry(BundleNode Node, JsonElement Resource);

    private static BundleGraph Build(string? type, List<Entry> entries)
    {
        var nodes = new List<BundleNode>();
        var byKey = new Dictionary<string, BundleNode>(StringComparer.Ordinal);
        var byFullUrl = new Dictionary<string, BundleNode>(StringComparer.Ordinal);
        var byReference = new Dictionary<string, BundleNode>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            // Two entries of one key (a history Bundle): the first stands for both
            if (!byKey.TryAdd(entry.Node.Key, entry.Node))
            {
                continue;
            }

            nodes.Add(entry.Node);
            if (entry.Node.FullUrl is { } fullUrl)
            {
                byFullUrl.TryAdd(StripHistory(fullUrl), entry.Node);
            }

            if (entry.Node.Reference is { } reference)
            {
                byReference.TryAdd(reference, entry.Node);
            }
        }

        var edges = new List<BundleEdge>();
        var seen = new HashSet<BundleEdge>();
        foreach (var entry in entries.Where(entry => byKey.GetValueOrDefault(entry.Node.Key) == entry.Node))
        {
            foreach (var found in References(entry.Resource, string.Empty))
            {
                if (Resolve(entry.Node, found.Reference, byFullUrl, byReference) is not { } target)
                {
                    if (Outside(found.Reference) is not { } outside)
                    {
                        continue;
                    }

                    if (!byKey.TryGetValue(outside.Key, out target))
                    {
                        target = outside;
                        byKey[target.Key] = target;
                        nodes.Add(target);
                    }
                }

                if (target.Key != entry.Node.Key && seen.Add(new BundleEdge(entry.Node.Key, target.Key, found.Path)))
                {
                    edges.Add(new BundleEdge(entry.Node.Key, target.Key, found.Path));
                }
            }
        }

        return new BundleGraph(type, nodes, edges);
    }

    private static string StripHistory(string reference)
    {
        var history = reference.IndexOf("/_history/", StringComparison.Ordinal);
        return history < 0 ? reference : reference[..history];
    }

    private static BundleNode? Resolve(BundleNode from, string reference, Dictionary<string, BundleNode> byFullUrl, Dictionary<string, BundleNode> byReference)
    {
        var target = StripHistory(reference);
        if (IsAbsolute(target))
        {
            return byFullUrl.GetValueOrDefault(target);
        }

        // Relative: against the base of the fullUrl of the entry when that is a RESTful address, else by type and id
        if (from.FullUrl is { } fullUrl && RestfulReference().Match(StripHistory(fullUrl)) is { Success: true } match && match.Groups["base"].Success &&
            byFullUrl.TryGetValue($"{match.Groups["base"].Value}/{target}", out var found))
        {
            return found;
        }

        return byReference.GetValueOrDefault(target);
    }

    private static bool IsAbsolute(string reference)
    {
        return reference.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(reference, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    // The node of a reference the Bundle does not hold: its type and id when the reference tells them; a urn names no type
    private static BundleNode? Outside(string reference)
    {
        var target = StripHistory(reference);
        if (RestfulReference().Match(target) is { Success: true } match)
        {
            return new BundleNode(target, match.Groups["type"].Value, match.Groups["id"].Value, IsAbsolute(target) ? target : null, null, false);
        }

        return target.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) ? new BundleNode(target, "Resource", null, target, null, false) : null;
    }

    private sealed record FoundReference(string Path, string Reference);

    // Every Reference.reference of a resource with the path of its element (the names of the members, without the indexes); not the contained
    // resources (their references are of their own) nor the narrative
    private static IEnumerable<FoundReference> References(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (path.Length > 0 && element.TryGetProperty("reference", out var reference) && reference.ValueKind == JsonValueKind.String &&
                    reference.GetString() is { Length: > 0 } text && text[0] != '#')
                {
                    yield return new FoundReference(path, text);
                }

                foreach (var member in element.EnumerateObject())
                {
                    if (path.Length == 0 && member.Name is "contained" or "text" or "meta")
                    {
                        continue;
                    }

                    // The reference itself; of a CodeableReference (R5) it is a Reference, read as any other member
                    if (member.Name == "reference" && member.Value.ValueKind == JsonValueKind.String)
                    {
                        continue;
                    }

                    foreach (var found in References(member.Value, path.Length == 0 ? member.Name : $"{path}.{member.Name}"))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var found in References(item, path))
                    {
                        yield return found;
                    }
                }

                break;
        }
    }

    // The part of the graph around a node: the nodes up to depth references away, either way
    public BundleGraph Around(string key, int depth)
    {
        var keep = new HashSet<string>(StringComparer.Ordinal) { key };
        var frontier = new List<string> { key };
        for (var step = 0; step < depth && frontier.Count > 0; step++)
        {
            var next = new List<string>();
            foreach (var edge in Edges)
            {
                if (frontier.Contains(edge.From) && keep.Add(edge.To))
                {
                    next.Add(edge.To);
                }
                else if (frontier.Contains(edge.To) && keep.Add(edge.From))
                {
                    next.Add(edge.From);
                }
            }

            frontier = next;
        }

        return Where(node => keep.Contains(node.Key));
    }

    // The graph without the nodes that do not match (and their references)
    public BundleGraph Where(Func<BundleNode, bool> predicate)
    {
        var nodes = Nodes.Where(predicate).ToList();
        var keys = nodes.Select(node => node.Key).ToHashSet(StringComparer.Ordinal);
        return new BundleGraph(Type, nodes, [.. Edges.Where(edge => keys.Contains(edge.From) && keys.Contains(edge.To))]);
    }
}
