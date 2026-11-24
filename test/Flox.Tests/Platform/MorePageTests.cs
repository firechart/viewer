using Flox.Shared.Components;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using Flox.Core.Branding;
using Flox.Core.Host;
using Flox.Core.Services;
using Flox.Shared.Errors;
using Microsoft.Extensions.DependencyInjection;
using MorePage = Flox.Shared.Pages.More.Index;

namespace Flox.Tests.Platform;

// The More page builds its menu for the user of the cascading authentication state, and again when the user signs in or out (BL0013)
public class MorePageTests
{
    private const string SignedInPolicy = "signed-in";

    [Test]
    public async Task TheMenu_IsBuiltAgain_WhenTheUserSignsIn()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddSingleton<IFloxShell>(new Shell());
        context.Services.AddSingleton<IFloxBrandingProvider>(new FloxBrandingProvider(new FloxBranding { Title = "Test", Icon = "icon.svg", Logo = "logo.svg" }));
        context.Services.AddSingleton(Null<IAuthenticationService>.Create());
        context.Services.AddSingleton(Null<IUiExceptionHandler>.Create());
        context.Services.AddSingleton(Null<IHttpClientFactory>.Create());

        var authorization = context.AddAuthorization();
        authorization.SetNotAuthorized();

        var page = context.Render<MorePage>();
        var before = page.Markup;

        authorization.SetPolicies(SignedInPolicy);
        authorization.SetAuthorized("u");

        Assert.Multiple(() =>
        {
            Assert.That(before, Does.Not.Contain("Settings of the account"));
            Assert.That(page.Markup, Does.Contain("Settings of the account"));
        });
    }

    private sealed class Shell : IFloxShell
    {
        public IReadOnlyList<Assembly> BlazorAssemblies { get; } = [];

        public IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user)
        {
            var menu = new FloxMenu("Account");
            menu.Items.Add(new FloxMenuItem("Settings of the account", href: "/account") { Policy = SignedInPolicy, Placement = FloxMenuPlacement.More });
            return [menu];
        }
    }

    // A service the page injects but this test never uses
    public class Null<T> : DispatchProxy where T : class
    {
        public static T Create()
        {
            return Create<T, Null<T>>();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask : null;
        }
    }
}
