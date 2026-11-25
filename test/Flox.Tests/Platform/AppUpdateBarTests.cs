using Bunit;
using Flox.Shared.Components;

namespace Flox.Tests.Platform;

// The update of an application as its service worker tells it: the files of a new version as they come, then Restart, which asks the waiting version to take
// over; the first install says it saves the application for offline use and goes when it is done
public class AppUpdateBarTests
{
    [Test]
    public async Task ANewVersion_CountsItsFiles_ThenRestartAppliesIt()
    {
        await using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./_content/Flox.Shared/flox-update.js");
        module.SetupVoid("watch", _ => true);
        var apply = module.SetupVoid("apply");
        var bar = context.Render<AppUpdateBar>();

        await bar.InvokeAsync(() => bar.Instance.Progress(new(Loaded: 3, Total: 12, Update: true, Done: false, Failed: false)));
        var downloading = bar.Find("[data-app-update=downloading]").TextContent;
        await bar.InvokeAsync(() => bar.Instance.Progress(new(Loaded: 12, Total: 12, Update: true, Done: true, Failed: false)));
        await bar.Find("[data-app-update-restart]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(downloading, Does.Contain("3 of 12 files"));
            Assert.That(apply.Invocations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TheFirstInstall_SaysItSavesForOfflineUse_AndGoesWhenDone()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var bar = context.Render<AppUpdateBar>();

        await bar.InvokeAsync(() => bar.Instance.Progress(new(Loaded: 1, Total: 4, Update: false, Done: false, Failed: false)));
        var saving = bar.Find("[data-app-update]").TextContent;
        await bar.InvokeAsync(() => bar.Instance.Progress(new(Loaded: 4, Total: 4, Update: false, Done: true, Failed: false)));

        Assert.Multiple(() =>
        {
            Assert.That(saving, Does.Contain("offline"));
            Assert.That(bar.FindAll("[data-app-update]"), Is.Empty);
        });
    }
}
