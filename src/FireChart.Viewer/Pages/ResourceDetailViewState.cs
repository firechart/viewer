using System.Net;
using FireChart.Viewer.Routing;
using Flox.Core.Util;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Pages;

/// <summary>
/// What the page of a resource shows.
/// </summary>
/// <param name="Session">The instance of the Viewer.</param>
/// <param name="ResourceType">The type of the resource.</param>
/// <param name="ResourceId">Its id.</param>
public sealed record ResourceDetailKey(ViewerSession Session, string ResourceType, string ResourceId);

/// <summary>
/// The state of the page of a resource: the resource as its view template shows it, with what the template asks to come with it, and its JSON.
/// Activated with a <see cref="ResourceDetailKey"/>.
/// </summary>
/// <example>
/// <code>
/// await state.ActivateAsync(new ResourceDetailKey(session, "Patient", "123"));
/// var html = state.View?.Html;
/// </code>
/// </example>
public class ResourceDetailViewState : State
{
    private readonly IServerCredentialStore _credentials;
    private readonly IFhirServerReader _reader;
    private readonly IServerResourceTypeProvider _types;
    private readonly IResourceViews _views;
    private readonly ITerminologyResolver _terminology;
    private ResourceDetailKey? _key;
    private ShownResource? _shown;

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceDetailViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _reader = serviceProvider.GetRequiredService<IFhirServerReader>();
        _types = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _views = serviceProvider.GetRequiredService<IResourceViews>();
        _terminology = serviceProvider.GetRequiredService<ITerminologyResolver>();
        _terminology.Resolved += OnTerminologyResolved;
    }

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>The resource as the server sent it, indented.</summary>
    public string? Json { get; private set; }

    /// <summary>The resource as its view template shows it; <see langword="null"/> when the type has none (the page shows the JSON).</summary>
    public ResourceView? View { get; private set; }

    /// <summary>The last load failed.</summary>
    public bool Failed { get; private set; }

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>
    /// The resource as a line of text (its <c>text</c> template): the title of a bookmark of the page.
    /// </summary>
    /// <returns>The text; <see langword="null"/> before the resource is read or when it has no text.</returns>
    /// <example>
    /// <code>
    /// var title = await state.TextAsync() ?? "Patient/123";
    /// </code>
    /// </example>
    public async Task<string?> TextAsync()
    {
        if (_shown is not { } shown || Server is null)
        {
            return null;
        }

        var text = await _views.RenderAsync(shown.Resource, shown.Related, shown.Release, Link, ViewerText.Mode, _key!.Session.TerminologyServer, Server);
        return ViewerText.Plain(text?.Html);
    }

    /// <summary>
    /// Reads the resource again.
    /// </summary>
    /// <returns>A task that completes when it is read.</returns>
    public async Task RetryAsync()
    {
        await LoadAsync();
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(object? key)
    {
        _key = (ResourceDetailKey)key!;
        await LoadAsync();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _terminology.Resolved -= OnTerminologyResolved;
        }

        base.Dispose(disposing);
    }

    // The answers of the terminology server that came after the page was drawn: the page is drawn again with them
    private void OnTerminologyResolved(object? sender, EventArgs e)
    {
        UiErrors.Fire(async () =>
        {
            if (_shown is not { } shown || Server is null)
            {
                return;
            }

            View = await _views.RenderAsync(shown.Resource, shown.Related, shown.Release, Link, IResourceViews.Mode, _key!.Session.TerminologyServer, Server);
            NotifyChanged();
        }, nameof(OnTerminologyResolved));
    }

    // "fhir:Type/id" in the HTML of a template: the address of the page of that resource in the Viewer
    private string? Link(string link)
    {
        var parts = link[FxHtml.FhirScheme.Length..].Split('/');
        return parts.Length == 2 && ViewerRoutes.Parse(new ViewerRoute(ViewerPage.Resource, parts[0], parts[1]).Location) is { } route ? _key!.Session.HrefOf(route) : null;
    }

    // The resource and what its view template asks to come with it (the patient, the encounter) in one request; a type without such a template is just read
    private async Task<ReadResource> ReadAsync(IViewerServer server)
    {
        var needs = await _views.NeedsAsync(_key!.ResourceType);
        if (!needs.IsEmpty)
        {
            var page = await _reader.SearchAsync(server, _key.ResourceType, FhirSearchState.Parse($"_id={Uri.EscapeDataString(_key.ResourceId)}"), needs);
            if (page.Items.FirstOrDefault(item => item.Id == _key.ResourceId) is { } found)
            {
                return new ReadResource(found, page.Related ?? []);
            }
        }

        return new ReadResource(new ResourceSummary(_key.ResourceId, _key.ResourceType, await _reader.ReadAsync(server, _key.ResourceType, _key.ResourceId)), []);
    }

    private sealed record ReadResource(ResourceSummary Resource, IReadOnlyList<RelatedResource> Related);

    private sealed record ShownResource(ResourceSummary Resource, IReadOnlyList<RelatedResource> Related, string? Release);

    private async Task LoadAsync()
    {
        var session = _key!.Session;
        await RunAsync(async () =>
        {
            NotFound = false;
            Failed = false;
            CredentialsRejected = false;
            Json = null;
            View = null;
            _shown = null;
            Server = session.Server;
            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                var read = await ReadAsync(session.Server);
                Json = JsonUtil.Beautify(read.Resource.Json!);
                var release = await _types.ReleaseOfAsync(session.Server);
                _shown = new ShownResource(read.Resource, read.Related, release);
                View = await _views.RenderAsync(read.Resource, read.Related, release, Link, IResourceViews.Mode, session.TerminologyServer, session.Server);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                NotFound = true;
            }
            catch (Exception ex) when (ex.IsRejected())
            {
                await ServiceProvider.GetRequiredService<ISmartAuthorizer>().DisconnectAsync(session.Server);

                _credentials.Forget(session.Server);
                CredentialsRejected = true;
            }
            catch
            {
                Failed = true;
                throw;
            }
        });
        NotifyChanged();
    }
}
