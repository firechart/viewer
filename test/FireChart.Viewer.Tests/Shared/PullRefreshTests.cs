using System.Security.Claims;
using Bunit;
using Flox.Core.Services;
using Flox.Services;
using Flox.Shared.Components;
using Flox.Shared.Errors;
using Flox.Shared.Layout;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Tests.Shared;

// A pull down of the page is handed to the states of the widgets shown (State.OnPullRefreshAsync): a list refreshes, a form is never asked
[TestFixture]
public class PullRefreshTests
{
    private const string Module = "./_content/Flox.Shared/Layout/PullRefreshHost.razor.js";

    public sealed class ListState(IServiceProvider services) : State(services)
    {
        public int Refreshes { get; private set; }

        public override bool RefreshesOnPull => true;

        public override Task OnPullRefreshAsync()
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }

    public sealed class FormState(IServiceProvider services) : State(services)
    {
        public int Refreshes { get; private set; }

        public override Task OnPullRefreshAsync()
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }

    public sealed class ListWidget : StatefulWidget<ListState>;

    public sealed class FormWidget : StatefulWidget<FormState>;

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

    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule(Module).Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddScoped<INavigationHistoryService, NavigationHistoryService>();
        context.Services.AddScoped<IAuthenticationService, Authentication>();
        context.Services.AddScoped<IUiExceptionHandler, Errors>();
        context.Services.AddHttpClient();
        context.Services.AddScoped<PullRefreshTargets>();
        context.Services.AddScoped<ListState>();
        context.Services.AddScoped<FormState>();
        return context;
    }

    [Test]
    public async Task APull_RefreshesTheStateOfAList_NeverAForm_AndAGoneListTurnsTheGestureOff()
    {
        using var context = Context();
        var list = context.Render<ListWidget>();
        var form = context.Render<FormWidget>();
        var host = context.Render<PullRefreshHost>();
        var targets = context.Services.GetRequiredService<PullRefreshTargets>();

        await host.InvokeAsync(host.Instance.OnPull);

        Assert.Multiple(() =>
        {
            Assert.That(list.Instance.State.Refreshes, Is.EqualTo(1));
            Assert.That(form.Instance.State.Refreshes, Is.Zero);
            Assert.That(targets.Any, Is.True, "the gesture is on for a list page");
        });

        list.Instance.Dispose();
        await host.InvokeAsync(host.Instance.OnPull);

        Assert.Multiple(() =>
        {
            Assert.That(targets.Any, Is.False, "a page without a list has no pull");
            Assert.That(list.Instance.State.Refreshes, Is.EqualTo(1));
        });
    }
}
