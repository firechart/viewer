
namespace FireChart.Viewer.Core;

// The pool of a rendering with the displays of the codes of its records, when a terminology server is named
internal static class TerminologyPool
{
    public static async Task AttachAsync(ITerminologyResolver? resolver, ResourcePool pool, string? server, IEnumerable<string> resources, TimeSpan wait, CancellationToken cancellationToken)
    {
        if (resolver is null || string.IsNullOrEmpty(server))
        {
            return;
        }

        var codes = TerminologyCodes.Collect(resources);
        if (codes.Count > 0)
        {
            pool.Terminology = await resolver.ResolveAsync(server, [.. codes], wait, cancellationToken);
        }
    }
}
