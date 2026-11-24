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
    private readonly IReleaseModels _models;
    private ResourceDetailKey? _key;
    private string? _release;

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceDetailViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _reader = serviceProvider.GetRequiredService<IFhirServerReader>();
        _types = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _models = serviceProvider.GetRequiredService<IReleaseModels>();
    }

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>The resource as the server sent it, indented.</summary>
    public string? Json { get; private set; }

    /// <summary>The resource as the server sent it: the components of the page draw it (<c>{Type}.page</c>, else its banner and its view).</summary>
    public string? Record { get; private set; }

    /// <summary>The last load failed.</summary>
    public bool Failed { get; private set; }

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>
    /// The resource as a line of text (what it is about: a code, a name, a title): the title of a bookmark of the page.
    /// </summary>
    /// <returns>The text; <see langword="null"/> before the resource is read or when it has no text.</returns>
    /// <example>
    /// <code>
    /// var title = await state.TextAsync() ?? "Patient/123";
    /// </code>
    /// </example>
    public async Task<string?> TextAsync()
    {
        if (Record is not { } json)
        {
            return null;
        }

        var model = await _models.GetAsync(_release);
        return FireChart.Viewer.Core.Reports.ReportText.TitleOf(FireChart.Viewer.Core.Reports.ReportExpressions.Parse(json, model), System.Globalization.CultureInfo.CurrentCulture);
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


    private async Task LoadAsync()
    {
        var session = _key!.Session;
        await RunAsync(async () =>
        {
            NotFound = false;
            Failed = false;
            CredentialsRejected = false;
            Json = null;
            Record = null;
            Server = session.Server;
            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                var json = await _reader.ReadAsync(session.Server, _key.ResourceType, _key.ResourceId);
                Json = JsonUtil.Beautify(json);
                _release = await _types.ReleaseOfAsync(session.Server);
                Record = json;
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
