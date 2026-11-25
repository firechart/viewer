using Bunit;
using FireChart.Viewer.Bundles;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Bundles;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Tests.Reports;
using System.Net;
using FireChart.Viewer.Core.Smart;
using FireChart.Viewer.Smart;
using Flox.Shared.Components;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Viewer;

// The Bundle Visualizer: the references of a Bundle resolved as the specification says, drawn in layers, and a resource opened in the sheet with its
// card and its source, without a request to any network
[TestFixture]
public class BundleVisualizerTests
{
    // A transaction of the kind a client sends: fullUrl urn:uuid, the references by them; a Location it does not hold; a contained resource
    private const string Transaction = """
        {
          "resourceType": "Bundle", "type": "transaction",
          "entry": [
            { "fullUrl": "urn:uuid:p", "resource": { "resourceType": "Patient", "id": "p1", "name": [ { "family": "Example", "given": [ "Alex" ] } ] } },
            { "fullUrl": "urn:uuid:e", "resource": { "resourceType": "Encounter", "id": "e1", "status": "finished",
                "class": { "code": "AMB" }, "subject": { "reference": "urn:uuid:p" },
                "location": [ { "location": { "reference": "Location/room-12" } } ] } },
            { "fullUrl": "urn:uuid:o", "resource": { "resourceType": "Observation", "id": "o1", "status": "final",
                "contained": [ { "resourceType": "Device", "id": "d", "owner": { "reference": "Organization/elsewhere" } } ],
                "device": { "reference": "#d" },
                "code": { "text": "Body weight" }, "subject": { "reference": "urn:uuid:p" }, "encounter": { "reference": "urn:uuid:e" },
                "valueQuantity": { "value": 71.4, "unit": "kg" } } }
          ]
        }
        """;

    [Test]
    public void References_ResolveByFullUrl_RelativeAgainstTheBaseOfTheEntry_AndWhatTheBundleLacksIsANodeOfItsOwn()
    {
        var graph = BundleGraph.Parse(Transaction);
        var searchset = BundleGraph.Parse("""
            { "resourceType": "Bundle", "type": "searchset", "entry": [
              { "fullUrl": "https://a.example/fhir/Patient/1", "resource": { "resourceType": "Patient", "id": "1" } },
              { "fullUrl": "https://b.example/fhir/Patient/1", "resource": { "resourceType": "Patient", "id": "1" } },
              { "fullUrl": "https://b.example/fhir/Observation/9", "resource": { "resourceType": "Observation", "id": "9", "subject": { "reference": "Patient/1/_history/2" } } } ] }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(graph.Edges.Select(edge => $"{edge.From} {edge.Path} {edge.To}"), Is.EquivalentTo(new[]
            {
                "urn:uuid:e subject urn:uuid:p", "urn:uuid:e location.location Location/room-12",
                "urn:uuid:o subject urn:uuid:p", "urn:uuid:o encounter urn:uuid:e"
            }), "the contained Device and its owner are not edges");
            Assert.That(graph.Find("Location/room-12"), Has.Property(nameof(BundleNode.InBundle)).False.And.Property(nameof(BundleNode.ResourceType)).EqualTo("Location"));
            Assert.That(graph.EntryCount, Is.EqualTo(3));
            Assert.That(searchset.Edges.Single().To, Is.EqualTo("https://b.example/fhir/Patient/1"), "the patient of the server of the observation, not the first of its id");
        });
    }

    [Test]
    public void ATextThatIsNotABundle_IsAFormatError()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => BundleGraph.Parse("""{ "resourceType": "Patient", "id": "p1" }"""), Throws.TypeOf<FormatException>());
            Assert.That(() => BundleGraph.Parse("{ not json"), Throws.TypeOf<FormatException>());
        });
    }

    // In R5 the reference of a CodeableReference is a Reference of its own (medication.reference.reference)
    [Test]
    public void TheReferenceOfACodeableReference_IsAnEdge()
    {
        var graph = BundleGraph.Parse("""
            { "resourceType": "Bundle", "type": "collection", "entry": [
              { "resource": { "resourceType": "Medication", "id": "m1" } },
              { "resource": { "resourceType": "MedicationRequest", "id": "r1", "medication": { "reference": { "reference": "Medication/m1" } } } } ] }
            """);

        Assert.That(graph.Edges.Select(edge => $"{edge.From} {edge.Path} {edge.To}"), Is.EqualTo(new[] { "MedicationRequest/r1 medication.reference Medication/m1" }));
    }

    // What refers to nothing is on top, what refers to it below; fifty observations of one encounter wrap into rows, not one row of fifty
    [Test]
    public void TheLayers_GoFromWhatIsReferredTo_Down_AndALongLayerWraps()
    {
        var observations = string.Join(",", Enumerable.Range(1, 50).Select(i =>
            $$"""{ "resource": { "resourceType": "Observation", "id": "o{{i}}", "subject": { "reference": "Patient/p1" }, "encounter": { "reference": "Encounter/e1" } } }"""));
        var graph = BundleGraph.Parse($$"""
            { "resourceType": "Bundle", "type": "collection", "entry": [
              { "resource": { "resourceType": "Patient", "id": "p1", "link": [ { "other": { "reference": "Patient/p2" } } ] } },
              { "resource": { "resourceType": "Patient", "id": "p2", "link": [ { "other": { "reference": "Patient/p1" } } ] } },
              { "resource": { "resourceType": "Encounter", "id": "e1", "subject": { "reference": "Patient/p1" } } },
              {{observations}} ] }
            """);

        var drawing = BundleLayout.Draw(graph);
        double Top(string key)
        {
            return drawing.Nodes.Single(node => node.Node.Key == key).Y;
        }

        Assert.Multiple(() =>
        {
            Assert.That(Top("Patient/p1"), Is.LessThan(Top("Encounter/e1")));
            Assert.That(Top("Encounter/e1"), Is.LessThan(Top("Observation/o1")));
            Assert.That(drawing.Nodes.Where(node => node.Node.ResourceType == "Observation").Select(node => node.Y).Distinct().Count(), Is.EqualTo(5));
            Assert.That(drawing.Edges, Has.Count.EqualTo(graph.Edges.Count), "the cycle of the two patients is drawn too");
        });
    }

    [Test]
    public async Task APastedBundle_IsDrawn_ItsResourceOpensWithItsCardAndSource_AndNothingIsAskedOfANetwork()
    {
        await using var context = new BunitContext();
        var network = new Network();
        var visualizer = Render(context, network, Transaction);

        visualizer.WaitForState(() => Drawn(visualizer, "urn:uuid:p", "Alex Example"), TimeSpan.FromSeconds(10));
        Assert.That(visualizer.Find("[data-viewer=bundle-counts]").TextContent, Does.Contain("3").And.Contain("4"));
        await Node(visualizer, "urn:uuid:e").ClickAsync(new());

        // The card is the tile of the encounter, its patient read from the Bundle by its urn
        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-card]").Any(card => card.TextContent.Contains("Alex Example", StringComparison.Ordinal)),
                                TimeSpan.FromSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(visualizer.Find("[data-viewer=bundle-refers-to]").TextContent, Does.Contain("subject").And.Contain("location.location"));
            Assert.That(visualizer.Find("[data-viewer=bundle-referred-by]").TextContent, Does.Contain("Body weight"));
            Assert.That(network.Requests, Is.Zero);
        });

        // A reference of the card chooses the resource it names
        await visualizer.FindAll("[data-viewer=bundle-referred-by] button").Single().ClickAsync(new());
        Assert.That(visualizer.Find("[data-viewer=bundle-sheet]").GetAttribute("aria-label"), Is.EqualTo("Observation"));

        // A resource the Bundle only refers to: its Source says why there is none, nothing is read to fill it
        await Node(visualizer, "Location/room-12").ClickAsync(new());
        await visualizer.Find("[data-viewer=bundle-source-tab]").ClickAsync(new());
        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-no-source]").Count == 1, TimeSpan.FromSeconds(10));
        Assert.That(network.Requests, Is.Zero);
    }

    // The sheet closes while its script is still being let go and the drawing renders again meanwhile (seen in the browser: a
    // NullReferenceException of FollowSheetAsync): the second render does not let go of the same sheet again
    [Test]
    public async Task TheSheetClosed_WhileARenderFollows_IsLetGoOnce()
    {
        await using var context = new BunitContext();
        var network = new Network();
        var dispose = context.JSInterop.SetupVoid("dispose");
        var visualizer = Render(context, network, Transaction);
        visualizer.WaitForState(() => Drawn(visualizer, "urn:uuid:p", "Alex Example"), TimeSpan.FromSeconds(10));
        await Node(visualizer, "urn:uuid:e").ClickAsync(new());
        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-sheet]").Count == 1, TimeSpan.FromSeconds(10));

        await visualizer.Find("[data-viewer=bundle-sheet-mask]").ClickAsync(new());
        visualizer.Render();
        dispose.SetVoidResult();

        visualizer.WaitForAssertion(() => Assert.That(visualizer.FindAll("[data-viewer=bundle-sheet]"), Is.Empty), TimeSpan.FromSeconds(10));
        visualizer.Render();
    }

    // An address of a server with a SMART client: the sign-in comes first and nothing is asked of the server before it; once signed in, the Bundle is
    // read with the token
    [Test]
    public async Task AnAddressOfAServerThatNeedsASignIn_IsReadAfterIt_WithItsToken()
    {
        await using var context = new BunitContext();
        var fhir = new FhirServer(HttpStatusCode.OK);
        var loads = new List<BundleLoad>();
        var visualizer = RenderAddress(context, fhir, loads);

        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-access]").Count == 1, TimeSpan.FromSeconds(10));
        Assert.That(fhir.Authorizations, Is.Empty, "nothing is read before the sign-in");

        context.Services.GetRequiredService<IServerCredentialStore>().SetAccessToken(SmartServer.Instance, "good", DateTimeOffset.UtcNow.AddHours(1));
        await visualizer.InvokeAsync(() => visualizer.FindComponent<ServerSignInPanel>().Instance.OnSignedIn.InvokeAsync(new SmartCompletion("smart", "/")));

        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-node]").Count == 4, TimeSpan.FromSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(fhir.Authorizations, Is.EqualTo(new[] { "Bearer good" }));
            Assert.That(loads, Has.Count.EqualTo(1).And.All.Property(nameof(BundleLoad.Succeeded)).True);
        });
    }

    // A token the server no longer takes (401) is forgotten with its session, and the sign-in is asked for again
    [Test]
    public async Task ATokenTheServerRefuses_IsForgotten_AndTheSignInIsAskedAgain()
    {
        await using var context = new BunitContext();
        var fhir = new FhirServer(HttpStatusCode.Unauthorized);
        var smart = new Smart();
        context.Services.AddSingleton<ISmartAuthorizer>(smart);
        var credentials = new ServerCredentialStore();
        credentials.SetAccessToken(SmartServer.Instance, "old", DateTimeOffset.UtcNow.AddHours(1));
        context.Services.AddSingleton<IServerCredentialStore>(credentials);
        var visualizer = RenderAddress(context, fhir, []);

        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-access]").Count == 1, TimeSpan.FromSeconds(10));
        Assert.Multiple(() =>
        {
            Assert.That(visualizer.Find("[data-viewer=bundle-access]").TextContent, Does.Contain("did not accept"));
            Assert.That(credentials.Has(SmartServer.Instance), Is.False);
            Assert.That(smart.Disconnected, Is.EqualTo(1));
        });
    }

    // The dialog of a loading that takes long: what it does, and Cancel, which leaves the Bundle out and tells the host; ReloadAsync reads it anew
    [Test]
    public async Task ALoadingCancelledInItsDialog_IsReportedAndReadAnewOnReload()
    {
        await using var context = new BunitContext();
        var fhir = new FhirServer(HttpStatusCode.OK) { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var credentials = new ServerCredentialStore();
        credentials.SetAccessToken(SmartServer.Instance, "good", DateTimeOffset.UtcNow.AddHours(1));
        context.Services.AddSingleton<IServerCredentialStore>(credentials);
        var loads = new List<BundleLoad>();
        var visualizer = RenderAddress(context, fhir, loads);

        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=loading-message]").Any(m => m.TextContent.Contains("fhir.example", StringComparison.Ordinal)),
                                TimeSpan.FromSeconds(10));
        await visualizer.Find("[data-viewer=cancel-loading]").ClickAsync(new());
        Assert.Multiple(() =>
        {
            Assert.That(visualizer.FindAll("[data-viewer=loading-panel]"), Is.Empty);
            Assert.That(loads.Select(load => load.Error), Is.EqualTo(new[] { BundleLoad.Cancelled }));
        });

        fhir.Gate.TrySetResult();
        await visualizer.Instance.ReloadAsync();
        visualizer.WaitForState(() => visualizer.FindAll("[data-viewer=bundle-node]").Count == 4, TimeSpan.FromSeconds(10));
        Assert.That(loads.Select(load => load.Error), Is.EqualTo(new[] { BundleLoad.Cancelled, null }));
    }

    private static IRenderedComponent<FhirBundleVisualizer> RenderAddress(BunitContext context, FhirServer fhir, List<BundleLoad> loads)
    {
        context.Services.AddHttpClient(FhirServerHttp.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => fhir);
        AddServices(context);
        return context.Render<FhirBundleVisualizer>(parameters => parameters.Add(visualizer => visualizer.Server, SmartServer.Instance)
                                                                           .Add(visualizer => visualizer.Location, "Encounter?_revinclude=Observation:encounter")
                                                                           .Add(visualizer => visualizer.Smart, new SmartOptions { RedirectUri = "https://app.example/smart/callback" })
                                                                           .Add(visualizer => visualizer.OnLoad, load => loads.Add(load)));
    }

    private static bool Drawn(IRenderedComponent<FhirBundleVisualizer> visualizer, string key, string title)
    {
        return visualizer.FindAll($"[data-viewer=bundle-node][data-key='{key}']").Any(node => node.TextContent.Contains(title, StringComparison.Ordinal));
    }

    private static AngleSharp.Dom.IElement Node(IRenderedComponent<FhirBundleVisualizer> visualizer, string key)
    {
        return visualizer.Find($"[data-viewer=bundle-node][data-key='{key}']");
    }

    private static IRenderedComponent<FhirBundleVisualizer> Render(BunitContext context, Network network, string bundle)
    {
        context.Services.AddSingleton<IHttpClientFactory>(network);
        AddServices(context);
        return context.Render<FhirBundleVisualizer>(parameters => parameters.Add(visualizer => visualizer.Bundle, bundle));
    }

    // What the host gives the visualizer; a service a test registered before stays
    private static void AddServices(BunitContext context)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.TryAddSingleton<IServerCredentialStore, ServerCredentialStore>();
        context.Services.TryAddSingleton<ISmartAuthorizer, Smart>();
        context.Services.TryAddSingleton<IViewerAssets>(new TextAssets(new Dictionary<string, string>()));
        context.Services.AddSingleton<IServerResourceTypeProvider, R4Types>();
        context.Services.AddSingleton<IReleaseModels, R4Models>();
        context.Services.AddSingleton<ITerminologyResolver, NoWords>();
        context.Services.AddSingleton<FireChart.Viewer.Reports.ViewerTerminology>();
        context.Services.AddSingleton<IViewerStyle, NoStyle>();
    }

    private sealed class NoStyle : IViewerStyle
    {
        public Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(":root{}");
        }
    }

    // A server with a SMART client
    private sealed class SmartServer : IViewerServer
    {
        public static readonly SmartServer Instance = new();

        public string Key => "smart";

        public string Name => "SMART";

        public string BaseUrl => "https://fhir.example/r4";

        public AuthenticationType AuthenticationType => AuthenticationType.OAuth2;

        public string? OAuth2ClientId => "client";

        public Hl7.Fhir.Specification.FhirRelease? FhirRelease => null;

        public int PageSize => 50;
    }

    // Answers every request with the transaction, or with a status; keeps the authorization of each
    private sealed class FhirServer(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];

        // Holds every answer until the test lets it go
        public TaskCompletionSource? Gate { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? Transaction : "{}", System.Text.Encoding.UTF8, "application/fhir+json")
            };
        }
    }

    // No session to restore; counts the sessions dropped
    private sealed class Smart : ISmartAuthorizer
    {
        public int Disconnected { get; private set; }

        public Task<string> StartAsync(IViewerServer server, string redirectUri, string returnUrl, string? launch, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SmartCompletion> CompleteAsync(string callbackUrl, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RestoreAsync(IViewerServer server, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task DisconnectAsync(IViewerServer server, CancellationToken cancellationToken = default)
        {
            Disconnected++;
            return Task.CompletedTask;
        }
    }

    // Counts what would leave the browser
    private sealed class Network : IHttpClientFactory
    {
        public int Requests { get; private set; }

        public HttpClient CreateClient(string name)
        {
            Requests++;
            throw new HttpRequestException("No network in this test.");
        }
    }

    private sealed class R4Models : IReleaseModels
    {
        public Task<ModelInspector> GetAsync(string? release, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModelInfo.ModelInspector);
        }
    }

    private sealed class R4Types : IServerResourceTypeProvider
    {
        public Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ServerResourceTypes("4.0.1", "R4", ["Patient"]));
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
}
