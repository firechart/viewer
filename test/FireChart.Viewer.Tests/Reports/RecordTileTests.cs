using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Reports;
using Microsoft.AspNetCore.Components;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Reports;

// A tile drawn again while it still waits for the component of its mode (a list or a card drawn again as its page loads) shows its record once: each
// render gave it a new link and it prepared again beside the first run, each run adding its row
public class RecordTileTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1", "name": [ { "text": "Alex Example" } ] }""";

    [Test]
    public async Task ATileDrawnAgainWhileItWaits_ShowsItsRecordOnce()
    {
        var assets = new SlowTiles(new TextAssets(new Dictionary<string, string> { ["B"] = """<Component name="B"><Text>b</Text></Component>""" }));
        var (_, _, scope) = await TestReport.LoadAsync(assets, "B", Patient, new TestReader(Patient, _ => []));
        var item = new ResourceSummary("p1", "Patient", Patient);
        RenderFragment tile = builder =>
        {
            builder.OpenComponent<RecordTile>(0);
            builder.AddComponentParameter(1, nameof(RecordTile.Item), item);
            builder.AddComponentParameter(2, nameof(RecordTile.Href), "resources/Patient/p1");
            builder.CloseComponent();
        };

        await using var context = new BunitContext();
        var host = context.Render<CascadingValue<ReportScope>>(parameters => parameters.Add(p => p.Value, scope).Add(p => p.ChildContent, tile));
        host.Render(parameters => parameters.Add(p => p.Value, scope).Add(p => p.ChildContent, tile));
        host.Render(parameters => parameters.Add(p => p.Value, scope).Add(p => p.ChildContent, tile));
        assets.Release();

        host.WaitForState(() => host.FindAll(".fx-summary-row").Count > 0, TimeSpan.FromSeconds(10));
        Assert.That(host.FindAll(".fx-summary-row"), Has.Count.EqualTo(1));
    }

    // A library whose components of a mode (a tile) come only when the test lets them
    private sealed class SlowTiles(IViewerAssets inner) : IViewerAssets
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release()
        {
            _gate.TrySetResult();
        }

        public async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
        {
            if (path.Contains(".tile", StringComparison.Ordinal))
            {
                await _gate.Task.WaitAsync(cancellationToken);
                return null;
            }

            return await inner.ReadTextAsync(path, cancellationToken);
        }
    }
}
