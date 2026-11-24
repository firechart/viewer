using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.Reports;
using Hl7.Fhir.Model;
using System.Globalization;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Reports;

internal sealed record TestServer : IViewerServer
{
    public string Key => "test";

    public string Name => "Test";

    public string BaseUrl => "https://fhir.test/r4";

    public AuthenticationType AuthenticationType => AuthenticationType.None;

    public Hl7.Fhir.Specification.FhirRelease? FhirRelease => null;

    public int PageSize => 20;
}

// A FHIR server for the reports: a search answers what Answer gives for its address, a read the record; each search is recorded. It answers when
// the test opens the gate, so the blocks are drawn first and wait, as they do over a network
internal sealed class TestReader(string record, Func<string, IReadOnlyList<ResourceSummary>> answer, Func<string, string, string?>? read = null,
                                 Func<string, IReadOnlyList<RelatedResource>>? related = null) : IFhirServerReader
{
    public List<string> Queries { get; } = [];

    // The records read, Type/id
    public List<string> Reads { get; } = [];

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ResourcePage> GetPageAsync(IViewerServer server, string link, string resourceType, CancellationToken cancellationToken = default)
    {
        lock (Queries)
        {
            Queries.Add(link);
        }

        await Gate.Task.WaitAsync(cancellationToken);
        var items = answer(link);
        return new ResourcePage(items, items.Count, null, null, null, null);
    }

    // The record of the report, or what read gives for another (none: the server answers 404)
    public Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default)
    {
        lock (Reads)
        {
            Reads.Add($"{resourceType}/{id}");
        }

        if (read is null)
        {
            return Task.FromResult(record);
        }

        return read(resourceType, id) is { } json
            ? Task.FromResult(json)
            : Task.FromException<string>(new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound));
    }

    public Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null,
                                          CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    // A first page of a feed: the answer to "Type?query", with the next link the test gives as "Type?query&next" (answered by GetPageAsync)
    public async Task<SortedResourcePage> SearchSortedAsync(IViewerServer server, string resourceType, FhirSearchState search, string sort, FhirNeeds? needs = null,
                                                            CancellationToken cancellationToken = default)
    {
        var link = $"{resourceType}?{search.ToSearchQuery(0)}";
        lock (Queries)
        {
            Queries.Add(link);
        }

        await Gate.Task.WaitAsync(cancellationToken);
        var items = answer(link);
        var next = answer(link + "&next").Count > 0 ? link + "&next" : null;
        return new SortedResourcePage(new ResourcePage(items, null, null, null, next, null, related?.Invoke(link)), true);
    }

    public Task<long?> CountAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}

// Components given as text, not files of the library
internal sealed class TextAssets(IReadOnlyDictionary<string, string> components) : IViewerAssets
{
    public Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var name = Path.GetFileNameWithoutExtension(path.Split('?')[0]);
        return Task.FromResult(components.GetValueOrDefault(name));
    }
}

// A report ready to render: its components checked, its searches started over the reader, the record as %context
internal sealed record TestReport(ReportBundle Bundle, ReportData Data, ReportScope Scope)
{
    public static async Task<TestReport> LoadAsync(IViewerAssets assets, string name, string record, IFhirServerReader reader, Func<string, string>? locate = null)
    {
        var library = new ReportLibrary(assets, locate ?? (path => path));
        var bundle = await library.LoadAsync(name);
        var data = new ReportData(reader, new TestServer(), ModelInfo.ModelInspector, CancellationToken.None);
        var english = CultureInfo.GetCultureInfo("en-US");
        var format = new ReportFormat(english, TimeZoneInfo.Local, strings: await library.StringsAsync(english));
        var runtime = new ReportRuntime(bundle.Components, data, format, component => library.LoadAsync(component));
        var scope = ReportScope.Root(runtime, ReportExpressions.Parse(record, ModelInfo.ModelInspector)).WithSearches(bundle.Root?.Data ?? []);
        return new TestReport(bundle, data, scope);
    }
}

// The files of the library, with components of the test over them
internal sealed class MergedAssets(IViewerAssets library, IReadOnlyDictionary<string, string> components) : IViewerAssets
{
    public Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var name = Path.GetFileNameWithoutExtension(path.Split('?')[0]);
        return components.TryGetValue(name, out var text) ? Task.FromResult<string?>(text) : library.ReadTextAsync(path, cancellationToken);
    }
}
