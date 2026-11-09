using System.Reflection;
using System.Security.Claims;
using Flox.Core.Host;
using Flox.Core.Replication;
using Flox.Core.Services;
using Flox.Shared.Host;
using Flox.Shared.Layout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Tests.Host;

// The navigation menu shows the main items and one More entry; the More page shows the rest. Both are built by MenuFilter
public class MoreMenuTests
{
    private sealed class FakeShell(params IFloxMenu[] menus) : IFloxShell
    {
        public IReadOnlyList<Assembly> BlazorAssemblies => [];

        public IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user)
        {
            return menus;
        }
    }

    // Allows every policy except the one named "denied"
    private sealed class FakeAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            return Task.FromResult(policyName == "denied" ? AuthorizationResult.Failed() : AuthorizationResult.Success());
        }
    }

    // What the host's container says about its services; with replication or without
    internal static IServiceProviderIsService ServicesOf(bool replicated = false)
    {
        var services = new ServiceCollection();
        if (replicated)
        {
            services.AddScoped<IReplicationService>(_ => throw new NotSupportedException());
        }

        return services.BuildServiceProvider()
                       .GetRequiredService<IServiceProviderIsService>();
    }

    private sealed class FakeRuntime(string environment) : FloxRuntime
    {
        public override FloxPlatform Platform => FloxPlatform.Wasm;

        public override string EnvironmentName => environment;
    }

    private static bool HasDiagnostics(string environment)
    {
        var menu = new SharedMenuSource(new FakeRuntime(environment), ServicesOf()).GetMenu(new ClaimsPrincipal(new ClaimsIdentity()));
        return menu.Items.Any(row => row.Href == SharedMenuItems.Diagnostics.Href);
    }

    private static readonly ClaimsPrincipal s_signedIn = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u")], "test"));

    private static FloxMenu MenuOf(string name, params IFloxMenuItem[] items)
    {
        var menu = new FloxMenu(name);
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        return menu;
    }

    private static async Task<List<string?>> TextsAsync(IFloxShell shell, FloxMenuPlacement placement)
    {
        var menus = await MenuFilter.BuildAsync(shell, new FakeAuthorization(), s_signedIn, placement);
        return menus.SelectMany(row => row.Nodes)
                    .Select(row => row.Item.Text)
                    .ToList();
    }

    private async Task TheItems_AreSplitByPlacement()
    {
        var shell = new FakeShell(MenuOf("app", new FloxMenuItem("Main item", href: "/a"), new FloxMenuItem("Rare item", href: "/b") { Placement = FloxMenuPlacement.More }));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await TextsAsync(shell, FloxMenuPlacement.Main), Is.EqualTo(new[] { "Main item" }));
            Assert.That(await TextsAsync(shell, FloxMenuPlacement.More), Is.EqualTo(new[] { "Rare item" }));
        });
    }

    private async Task AMenuWithNothingToShow_IsLeftOut()
    {
        var shell = new FakeShell(MenuOf("only main", new FloxMenuItem("A", href: "/a")), MenuOf("denied", new FloxMenuItem("B", href: "/b") { Placement = FloxMenuPlacement.More, Policy = "denied" }));

        var more = await MenuFilter.BuildAsync(shell, new FakeAuthorization(), s_signedIn, FloxMenuPlacement.More);

        Assert.That(more, Is.Empty, "no More entry in the navigation menu, and an empty More page");
    }

    private async Task AGroup_IsHiddenWhenAllItsChildrenAre()
    {
        var group = new FloxMenuItem("Group", href: "/g") { Placement = FloxMenuPlacement.More };
        group.Items.Add(new FloxMenuItem("Child", href: "/g/c") { Policy = "denied" });
        var shell = new FakeShell(MenuOf("app", group));

        Assert.That(await TextsAsync(shell, FloxMenuPlacement.More), Is.Empty);
    }

    private async Task ThePlatformMenu_KeepsTheEverydayItemsAndMovesTheRestToMore()
    {
        var shell = new FakeShell(new SharedMenuSource(new FakeRuntime("Development"), ServicesOf()).GetMenu(s_signedIn));

        var main = await TextsAsync(shell, FloxMenuPlacement.Main);
        var more = await TextsAsync(shell, FloxMenuPlacement.More);

        Assert.Multiple(() =>
        {
            Assert.That(main, Does.Contain(SharedMenuItems.Settings.Text));
            Assert.That(main, Does.Contain(SharedMenuItems.Bookmarks.Text));
            Assert.That(main, Does.Not.Contain(SharedMenuItems.Tasks.Text));
            Assert.That(more, Does.Contain(SharedMenuItems.Tasks.Text));
            Assert.That(more, Does.Contain(SharedMenuItems.About.Text));
        });
    }

    private async Task TheSyncPage_IsOfferedToASignedInUser_OnlyWhereTheApplicationReplicates()
    {
        var withReplication = new FakeShell(new SharedMenuSource(new FakeRuntime("Production"), ServicesOf(true)).GetMenu(s_signedIn));
        var without = new FakeShell(new SharedMenuSource(new FakeRuntime("Production"), ServicesOf()).GetMenu(s_signedIn));
        var visitor = new FakeShell(new SharedMenuSource(new FakeRuntime("Production"), ServicesOf(true)).GetMenu(new ClaimsPrincipal(new ClaimsIdentity())));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await TextsAsync(withReplication, FloxMenuPlacement.More), Does.Contain(SharedMenuItems.Sync.Text));
            Assert.That(await TextsAsync(without, FloxMenuPlacement.More), Does.Not.Contain(SharedMenuItems.Sync.Text), "no local store, nothing to monitor");
            Assert.That(await TextsAsync(visitor, FloxMenuPlacement.More), Does.Not.Contain(SharedMenuItems.Sync.Text), "nobody is signed in");
        });
    }

    // The diagnostics pages are in the menu of a debug build in the development environment, and nowhere else
    private void TheDiagnosticsPages_AreOnlyInADebugBuildInDevelopment()
    {
#if DEBUG
        Assert.That(HasDiagnostics("Development"), Is.True);
#else
        Assert.That(HasDiagnostics("Development"), Is.False, "a release build never shows it");
#endif
        Assert.That(HasDiagnostics("Production"), Is.False);
        Assert.That(HasDiagnostics("Staging"), Is.False);
    }

    [Test]
    public async Task MenuScenario()
    {
        await Assert.MultipleAsync(async () =>
        {
            await TheItems_AreSplitByPlacement();
            await AMenuWithNothingToShow_IsLeftOut();
            await AGroup_IsHiddenWhenAllItsChildrenAre();
            await ThePlatformMenu_KeepsTheEverydayItemsAndMovesTheRestToMore();
            await TheSyncPage_IsOfferedToASignedInUser_OnlyWhereTheApplicationReplicates();
            TheDiagnosticsPages_AreOnlyInADebugBuildInDevelopment();
        });
    }
}
