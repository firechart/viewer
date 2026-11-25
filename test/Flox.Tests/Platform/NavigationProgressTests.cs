using Bunit;
using Flox.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Tests.Platform;

// The bar under the app bar answers a navigation at once and lasts while the new page loads, not a fixed time
public class NavigationProgressTests
{
    [Test]
    public async Task TheProgress_StartsWithTheNavigation_AndEndsWhenThePageHasLoaded()
    {
        await using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        using var progress = new NavigationProgress(navigation);
        var page = new object();

        navigation.NavigateTo("server/1/resources/Encounter/e1");
        var atOnce = progress.Active;
        progress.SetBusy(page, true);
        await Task.Delay(400);
        var whileLoading = progress.Active;
        progress.SetBusy(page, false);
        await Task.Delay(400);

        Assert.Multiple(() =>
        {
            Assert.That(atOnce, Is.True);
            Assert.That(whileLoading, Is.True);
            Assert.That(progress.Active, Is.False);
        });
    }
}
