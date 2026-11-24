using Flox.Shared.Components;
using System.Security.Claims;
using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Smart;
using FireChart.Viewer.Pages;
using FireChart.Viewer.Routing;
using FireChart.Viewer.Tests.Reports;
using Flox.Core.Data;
using Flox.Core.Services;
using Flox.Services;
using Flox.Shared.Errors;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireChart.Viewer.Tests.Viewer;

// The types of a server are chosen in the mode of the eye of the header: a hidden type is gone from the page once the mode ends, also on the next visit
[TestFixture]
public class ResourceTypesViewTests
{
    private static IRenderedComponent<ResourceTypesView> Render(BunitContext context, ILocalDocumentStore device)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddScoped<INavigationHistoryService, NavigationHistoryService>();
        context.Services.AddScoped<IAuthenticationService, Authentication>();
        context.Services.AddScoped<IUiExceptionHandler, Errors>();
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IServerCredentialStore, ServerCredentialStore>();
        context.Services.AddSingleton<ISmartAuthorizer, NoSmart>();
        context.Services.AddSingleton<IViewerStyle, NoStyle>();
        context.Services.AddSingleton<IServerResourceTypeProvider, Types>();
        context.Services.AddScoped<IGuidEntityStorage<ResourceTypeVisibility>>(_ => new LocalGuidEntityStorage<ResourceTypeVisibility>(device, NullLogger<LocalEntityStorage<Guid, ResourceTypeVisibility>>.Instance));
        context.Services.AddScoped<ResourceTypesViewState>();
        var session = new ViewerSession(new TestServer(), new StartLocation()) { Bookmarks = false };
        return context.Render<CascadingValue<ViewerSession>>(parameters => parameters.Add(value => value.Value, session)
                                                                                     .AddChildContent<ResourceTypesView>())
                      .FindComponent<ResourceTypesView>();
    }

    private static string[] Shown(IRenderedComponent<ResourceTypesView> view)
    {
        return [.. view.FindAll("[data-slot=item-title]").Select(title => title.TextContent.Trim())];
    }

    [Test]
    public async Task AHiddenType_IsGoneOnceTheModeEnds_AndOnTheNextVisit()
    {
        var device = new InMemoryDocumentStore();
        await using (var context = new BunitContext())
        {
            var view = Render(context, device);
            view.WaitForAssertion(() => Assert.That(Shown(view), Is.EqualTo(new[] { "Patient", "Account", "Group" })), TimeSpan.FromSeconds(10));
            var state = view.Instance.State;

            await view.InvokeAsync(state.ToggleConfiguringAsync);
            await view.InvokeAsync(() => state.SetAllHidden(true));
            await view.Find("[data-viewer=hide-type][aria-label='Show Patient']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            Assert.That(Shown(view), Is.EqualTo(new[] { "Patient", "Account", "Group" }), "while the types are chosen, a hidden one is listed");
            Assert.That(view.FindAll("[data-viewer=hide-type]").Select(mark => mark.GetAttribute("aria-checked")), Is.EqualTo(new[] { "true", "false", "false" }),
                "the mark of a shown type is checked");

            await view.InvokeAsync(state.ToggleConfiguringAsync);
            Assert.That(Shown(view), Is.EqualTo(new[] { "Patient" }));
        }

        await using var again = new BunitContext();
        var next = Render(again, device);
        next.WaitForAssertion(() => Assert.That(Shown(next), Is.EqualTo(new[] { "Patient" })), TimeSpan.FromSeconds(10));
    }

    private sealed class Types : IServerResourceTypeProvider
    {
        public Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ServerResourceTypes("4.0.1", "R4", ["Group", "Account", "Patient"]));
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

    private sealed class StartLocation : IViewerLocationStore
    {
        public string? Location => string.Empty;

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
