using Bunit;
using Flox.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Tests.Platform;

// Back of the app bar is the Back of the browser: a record opened by a link from a record goes back to that record, not to a list the depth of the
// address made up; a section of the menu and a page opened first in the tab have the menu instead
public class NavigationHistoryTests
{
    [Test]
    public async Task Back_IsTheBrowsersBack_OnAPageTheTabCameTo_BelowASection()
    {
        await using var context = new BunitContext();
        var index = context.JSInterop.Setup<int>("floxHistory.index");
        var back = context.JSInterop.SetupVoid("floxHistory.back");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        using var history = new NavigationHistoryService(navigation, context.JSInterop.JSRuntime);

        index.SetResult(0);
        navigation.NavigateTo("server/1/resources/Practitioner/p1");
        var openedFirst = history.CanGoBack;
        index.SetResult(2);
        navigation.NavigateTo("server/1/resources/Practitioner/p1?view=source");
        var cameTo = history.CanGoBack;
        history.GoBack();
        navigation.NavigateTo("server");
        var section = history.CanGoBack;

        Assert.Multiple(() =>
        {
            Assert.That(openedFirst, Is.False);
            Assert.That(cameTo, Is.True);
            Assert.That(back.Invocations, Has.Count.EqualTo(1));
            Assert.That(section, Is.False);
        });
    }
}
