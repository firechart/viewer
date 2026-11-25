using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Pages;
using FireChart.Viewer.Reports;
using FireChart.Viewer.Tests.Reports;
using Flox.Shared.Components;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Viewer;

// How a summary loads: the page size of a search (the form, else the option, else 20, within the range); the whole summary first unless the host
// turns LoadAll off; its dialog is LoadingPanel of Flox
public class SummaryLoadingTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1" }""";

    private const string Summary = """
        <Component name="Patient.summary">
          <Feed code="problems" title="Problem list">
            <Query type="Condition"/>
          </Feed>
        </Component>
        """;

    [Test]
    public void ThePageSizeOfASearch_IsTheForms_ElseTheOption_ElseTheDefault_WithinTheRange()
    {
        var own = new SummaryFormSearch { ResourceType = "Observation", PageSize = 5 };
        var none = new SummaryFormSearch { ResourceType = "Condition" };

        Assert.Multiple(() =>
        {
            Assert.That(new SectionSearch(own, "en", pageSize: 100).PageSize, Is.EqualTo(5));
            Assert.That(new SectionSearch(none, "en", pageSize: new FhirOptions { PageSize = 100 }.EffectivePageSize()).PageSize, Is.EqualTo(100));
            Assert.That(new SectionSearch(none, "en").PageSize, Is.EqualTo(SectionSearch.DefaultPageSize));
            Assert.That(new FhirOptions { PageSize = 5000 }.EffectivePageSize(), Is.EqualTo(FhirOptions.MaxPageSize));
        });
    }

    // A host that gives no FHIR settings gets the whole summary first; LoadAll = false draws each section as it comes
    [TestCase(null, true)]
    [TestCase(false, false)]
    public async Task ASummary_IsHiddenUntilEverySectionCame_UnlessTheHostTurnsLoadAllOff(bool? loadAll, bool hidden)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var reader = new TestReader(Patient, _ => []);
        context.Services.AddSingleton<IFhirServerReader>(reader);
        context.Services.AddSingleton<IViewerAssets>(new TextAssets(new Dictionary<string, string> { ["Patient.summary"] = Summary }));
        context.Services.AddSingleton<IServerResourceTypeProvider, R4Types>();
        context.Services.AddSingleton<IReleaseModels, R4Models>();
        context.Services.AddSingleton<ITerminologyResolver, NoWords>();
        context.Services.AddSingleton<ViewerTerminology>();
        context.Services.AddSingleton<IViewerStyle, NoStyle>();
        context.Services.AddSingleton<ViewerSummaries>();
        context.SetRendererInfo(new RendererInfo("WebAssembly", true));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("summary/Patient/p1");

        var viewer = context.Render<FhirViewer>(parameters => parameters.Add(component => component.Server, new TestServer())
                                                                         .Add(component => component.ShowBreadcrumbs, false)
                                                                         .Add(component => component.Fhir, loadAll is { } value ? new FhirOptions { LoadAll = value } : null));
        viewer.WaitForState(() => viewer.FindAll("[data-report] [data-report-loading]").Count == 1);
        var waiting = viewer.Find("[data-report]").GetAttribute("aria-hidden");
        reader.Gate.SetResult();
        viewer.WaitForState(() => viewer.FindAll(".fx-summary-group-title").Count == 1 && viewer.Find("[data-report]").GetAttribute("aria-hidden") is null);

        Assert.That(waiting, Is.EqualTo(hidden ? "true" : null), "while the server has not answered");
    }

    private sealed class R4Types : IServerResourceTypeProvider
    {
        public Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ServerResourceTypes("4.0.1", "R4", ["Patient", "Condition"]));
        }
    }

    private sealed class R4Models : IReleaseModels
    {
        public Task<ModelInspector> GetAsync(string? release, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModelInfo.ModelInspector);
        }
    }

    private sealed class NoWords : ITerminologyResolver
    {
        public event EventHandler? Resolved
        {
            add { }
            remove { }
        }

        public Task<TerminologyMap> ResolveAsync(string serverAddress, IReadOnlyCollection<CodeKey> codes, TimeSpan wait, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoStyle : IViewerStyle
    {
        public Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(":root{}");
        }
    }
}
