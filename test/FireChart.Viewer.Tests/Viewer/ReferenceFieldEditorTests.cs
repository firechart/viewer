using Flox.Shared.Components;
using Bunit;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Routing;
using FireChart.Viewer.SearchForms;
using FireChart.Viewer.Tests.Reports;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using BlazorBlueprint.Components;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Viewer;

// A reference field shows the record it holds by its name, offers the records opened in the session, and searches the type as the person types
[TestFixture]
public class ReferenceFieldEditorTests
{
    private const string Smith = """{ "resourceType": "Patient", "id": "p1", "name": [ { "family": "Smith", "given": [ "Adolph" ] } ] }""";
    private const string Mercado = """{ "resourceType": "Patient", "id": "p2", "name": [ { "family": "Mercado", "given": [ "Virginia" ] } ] }""";

    private static SearchFormField Subject()
    {
        var field = new SearchFormField { QueryName = "subject", ParameterType = SearchParameterType.Reference, Control = SearchFieldControl.Reference };
        field.Lookup = new ReferenceLookup();
        field.Lookup.TargetTypes.AddRange(["Group", "Patient"]);
        return field;
    }

    private static IRenderedComponent<ReferenceFieldEditor> Render(BunitContext context, SearchFieldValue value, Server server, string location)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddSingleton<IFhirServerReader>(server);
        context.Services.AddSingleton<IReleaseModels, R4Models>();
        context.Services.AddSingleton<IServerResourceTypeProvider, R4Types>();
        var session = new ViewerSession(new TestServer(), new FixedLocation(location));
        return context.Render<ReferenceFieldEditor>(parameters => parameters.Add(editor => editor.Field, Subject()).Add(editor => editor.Value, value)
                                                                             .Add(editor => editor.Server, new TestServer()).Add(editor => editor.Session, session));
    }

    // The list of records of the field: the options the combobox of Blazor Blueprint is given (it draws them in a popover only while open)
    private static IRenderedComponent<BbCombobox<string>> Box(IRenderedComponent<ReferenceFieldEditor> editor)
    {
        return editor.FindComponents<BbCombobox<string>>()[^1];
    }

    private static string[] Options(IRenderedComponent<ReferenceFieldEditor> editor)
    {
        return [.. (Box(editor).Instance.Options ?? []).Select(option => option.Text)];
    }

    [Test]
    public async Task TheRecordHeld_IsShownByItsName_AndARecordOpenedInTheSession_IsOffered()
    {
        await using var context = new BunitContext();
        var server = new Server();

        var held = Render(context, new SearchFieldValue { Text = "Patient/p1" }, server, "resources/Patient/p2");
        held.WaitForAssertion(() => Assert.That(Options(held), Has.Some.StartsWith("Adolph Smith")), TimeSpan.FromSeconds(10));
        await using var other = new BunitContext();
        var empty = Render(other, new SearchFieldValue(), server, "summary/Patient/p2");
        empty.WaitForAssertion(() => Assert.That(Options(empty), Has.One.StartsWith("Virginia Mercado").And.Contains(FireChart.Resources.Strings.Caption_RecentRecords)), TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task WhatIsTyped_SearchesThePatientsByName_AndTheChoiceIsTheReference()
    {
        await using var context = new BunitContext();
        var server = new Server();
        var value = new SearchFieldValue();
        var editor = Render(context, value, server, "resources/Encounter");

        await editor.InvokeAsync(() => Box(editor).Instance.SearchQueryChanged.InvokeAsync("Smi"));
        editor.WaitForAssertion(() => Assert.That(Options(editor), Is.EqualTo(new[] { "Adolph Smith" })), TimeSpan.FromSeconds(10));
        var chosen = Box(editor).Instance.Options!.Single().Value;
        await editor.InvokeAsync(() => Box(editor).Instance.ValueChanged.InvokeAsync(chosen));

        Assert.Multiple(() =>
        {
            Assert.That(server.Searches, Is.EqualTo(new[] { "Patient?name=Smi&_count=10" }));
            Assert.That(value.Text, Is.EqualTo("Patient/p1"));
        });
    }

    private sealed class FixedLocation(string location) : IViewerLocationStore
    {
        public string? Location => location;

        public event EventHandler? LocationChanged
        {
            add { }
            remove { }
        }

        public void NavigateTo(string location, bool replace = false)
        {
        }

        public string HrefOf(string location)
        {
            return location;
        }

        public void Dispose()
        {
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
            return Task.FromResult(new ServerResourceTypes("4.0.1", "R4", ["Patient", "Group", "Encounter"]));
        }
    }

    // Two patients: Smith is found by "Smi", each is read by its id
    private sealed class Server : IFhirServerReader
    {
        public List<string> Searches { get; } = [];

        public Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null,
                                              CancellationToken cancellationToken = default)
        {
            Searches.Add($"{resourceType}?{search?.ToSearchQuery(0)}");
            IReadOnlyList<ResourceSummary> found = search?.ToSearchQuery(0).Contains("name=Smi", StringComparison.Ordinal) == true ? [new ResourceSummary("p1", "Patient", Smith)] : [];
            return Task.FromResult(new ResourcePage(found, found.Count, null, null, null, null));
        }

        public Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default)
        {
            return id switch
            {
                "p1" => Task.FromResult(Smith),
                "p2" => Task.FromResult(Mercado),
                _ => Task.FromException<string>(new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound))
            };
        }

        public Task<SortedResourcePage> SearchSortedAsync(IViewerServer server, string resourceType, FhirSearchState search, string sort, FhirNeeds? needs = null,
                                                          CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ResourcePage> GetPageAsync(IViewerServer server, string link, string resourceType, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<long?> CountAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
