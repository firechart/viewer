using System.Net;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Search;
using Hl7.Fhir.Specification;

namespace FireChart.Viewer.Core.Bundles;

// The server of the components that draw the resources of a Bundle: the Bundle itself. Nothing is asked of a network; a record the Bundle does not
// hold is not found, and a search finds nothing (a Bundle pasted stays in the browser)
internal sealed class BundleReader : IFhirServerReader
{
    public static readonly BundleReader Instance = new();

    private static HttpRequestException NotInBundle()
    {
        return new HttpRequestException("The record is not in the Bundle.", null, HttpStatusCode.NotFound);
    }

    public Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null,
                                          CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResourcePage([], 0, null, null, null, null));
    }

    public async Task<SortedResourcePage> SearchSortedAsync(IViewerServer server, string resourceType, FhirSearchState search, string sort, FhirNeeds? needs = null,
                                                            CancellationToken cancellationToken = default)
    {
        return new SortedResourcePage(await SearchAsync(server, resourceType, search, needs, cancellationToken), true);
    }

    public Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default)
    {
        return Task.FromException<string>(NotInBundle());
    }

    public Task<long?> CountAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<long?>(0);
    }

    public Task<ResourcePage> GetPageAsync(IViewerServer server, string link, string resourceType, CancellationToken cancellationToken = default)
    {
        return SearchAsync(server, resourceType, cancellationToken: cancellationToken);
    }
}

// The Bundle as the server of its components: the base of its address (an absolute reference to it is one of the Bundle), the release assumed
internal sealed class BundleServer(string baseUrl, FhirRelease release) : IViewerServer
{
    // The base of a pasted Bundle: no address of a network
    public const string PastedBase = "urn:firechart:bundle";

    public string Key => "bundle";

    public string Name => "Bundle";

    public string BaseUrl => baseUrl;

    public AuthenticationType AuthenticationType => AuthenticationType.None;

    public FhirRelease? FhirRelease => release;

    public int PageSize => 50;
}
