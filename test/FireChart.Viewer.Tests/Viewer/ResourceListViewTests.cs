using System.Net;
using System.Security.Claims;
using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Core.Smart;
using FireChart.Viewer.Pages;
using FireChart.Viewer.Routing;
using FireChart.Viewer.Tests.Reports;
using Flox.Core.Data;
using Flox.Core.Services;
using Flox.Services;
using Flox.Shared.Components;
using Flox.Shared.Errors;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Viewer;

// A search the server refuses (a parameter it does not support) is no dead end: the list says what the server said, keeps the chips and the filter,
// and clears the search so the list does not take the refused one again
[TestFixture]
public class ResourceListViewTests
{
    private const string Outcome = "Unknown search parameter \"foo\" for resource type \"Patient\"";

    [Test]
    public async Task ARefusedSearch_ShowsWhatTheServerSaid_KeepsTheFilters_AndIsCleared()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddScoped<INavigationHistoryService, NavigationHistoryService>();
        context.Services.AddScoped<IAuthenticationService, Authentication>();
        context.Services.AddScoped<IUiExceptionHandler, Errors>();
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IServerCredentialStore, ServerCredentialStore>();
        context.Services.AddSingleton<ISmartAuthorizer, NoSmart>();
        context.Services.AddSingleton<IViewerStyle, NoStyle>();
        context.Services.AddSingleton<IServerResourceTypeProvider, R4Types>();
        context.Services.AddSingleton<IReleaseModels, R4Models>();
        context.Services.AddSingleton<IViewerAssets>(new TextAssets(new Dictionary<string, string>()));
        context.Services.AddSingleton<IFhirServerReader, Refusing>();
        context.Services.AddSingleton<IValueSetExpander, ValueSetExpander>();
        context.Services.AddSingleton<SearchForms.ViewerSearchForms>();
        context.Services.AddSingleton<ITerminologyResolver, NoWords>();
        context.Services.AddSingleton<FireChart.Viewer.Reports.ViewerTerminology>();
        var memory = new LocalListSearchMemory(new InMemoryDocumentStore());
        context.Services.AddSingleton<IListSearchMemory>(memory);
        context.Services.AddScoped<ResourceListViewState>();
        var location = new RecordedLocation();
        var session = new ViewerSession(new TestServer(), location) { Bookmarks = false };

        var view = context.Render<CascadingValue<ViewerSession>>(parameters => parameters.Add(value => value.Value, session)
                                                                                      .AddChildContent<ResourceListView>(list => list.Add(l => l.ResourceType, "Patient")
                                                                                                                                       .Add(l => l.Query, "foo=bar&family=Smith")))
                          .FindComponent<ResourceListView>();

        // The host shows what the server said once it is told: it does not wait for tiles that never come
        view.WaitForState(() => view.FindAll("[data-report-host=ready] [data-search-rejected]").Count == 1, TimeSpan.FromSeconds(10));
        Assert.That(view.Find("[data-search-rejected]").TextContent, Does.Contain(Outcome));
        var state = view.Instance.State;
        Assert.That(state.ActiveFilters.Select(filter => filter.Parameter.Name), Is.EquivalentTo(new[] { "foo", "family" }), "the chips stay");
        Assert.That(state.CanFilter, "the filter stays");

        await view.InvokeAsync(state.ClearSearchAsync);
        Assert.That(location.Navigations[^1], Is.EqualTo("resources/Patient"));
        Assert.That(await memory.GetAsync(new TestServer(), "Patient"), Is.Empty, "the cleared search is kept, not the refused one");
    }

    private sealed class Refusing : IFhirServerReader
    {
        public Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null,
                                              CancellationToken cancellationToken = default)
        {
            return Task.FromException<ResourcePage>(new FhirRequestException("400 Bad Request", Outcome, HttpStatusCode.BadRequest));
        }

        public Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

    private sealed class RecordedLocation : IViewerLocationStore
    {
        public List<string> Navigations { get; } = [];

        public string? Location => "resources/Patient?foo=bar&family=Smith";

        public event EventHandler? LocationChanged
        {
            add { }
            remove { }
        }

        public void NavigateTo(string location, bool replace = false)
        {
            Navigations.Add(location);
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

    private sealed class NoStyle : IViewerStyle
    {
        public Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(":root{}");
        }
    }

    private sealed class NoSmart : ISmartAuthorizer
    {
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
            return Task.CompletedTask;
        }
    }

    private sealed class Authentication : IAuthenticationService
    {
        public event AuthenticationStateChangedHandler? AuthenticationStateChanged
        {
            add { }
            remove { }
        }

        public Task<string?> GetAccessTokenAsync()
        {
            return Task.FromResult<string?>(null);
        }

        public Task<ClaimsPrincipal> GetCurrentUserAsync()
        {
            return Task.FromResult(new ClaimsPrincipal());
        }

        public Task GoToProfileAsync()
        {
            return Task.CompletedTask;
        }

        public Task LoginAsync(string? returnUrl = null)
        {
            return Task.CompletedTask;
        }

        public Task LogoutAsync()
        {
            return Task.CompletedTask;
        }

        public void SetCurrentUser(ClaimsPrincipal? value)
        {
        }
    }

    private sealed class Errors : IUiExceptionHandler
    {
        public Task<UiExceptionContext> HandleAsync(Exception exception, string? operation = null, UiExceptionSource source = UiExceptionSource.Operation, string? userMessage = null)
        {
            return Task.FromException<UiExceptionContext>(exception);
        }

        public void Fire(Func<Task> action, string? operation = null)
        {
            _ = action();
        }
    }
}
