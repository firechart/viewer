using Bunit;
using Flox.Shared.Components;

namespace Flox.Tests.Platform;

// The dialog of a long loading: how far it is when the parts are counted, what it does when they are not, and Cancel only where the host can stop it
public class LoadingPanelTests
{
    [Test]
    public async Task ThePanel_TellsHowFarTheLoadingIs_AndCancelStopsIt()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var cancelled = 0;

        var panel = context.Render<LoadingPanel>(parameters => parameters.Add(p => p.Done, 2)
                                                                          .Add(p => p.Total, 5)
                                                                          .Add(p => p.Messages, ["Loaded: Conditions (3)", "Loaded: Encounters (12)"])
                                                                          .Add(p => p.OnCancel, () => cancelled++));
        await panel.Find("[data-viewer=cancel-loading]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(panel.Markup, Does.Contain("2 of 5 parts loaded").And.Contain("Loaded: Encounters (12)"));
            Assert.That(panel.Find("[role=status]"), Is.Not.Null);
            Assert.That(cancelled, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ALoadingOfUnknownLength_RunsItsBar_SaysWhatItDoes_AndHasNoCancelWithoutTheHost()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();

        var panel = context.Render<LoadingPanel>(parameters => parameters.Add(p => p.Title, "Loading the Bundle").Add(p => p.Message, "Reading it from fhir.example"));

        Assert.Multiple(() =>
        {
            Assert.That(panel.Find("[data-viewer=loading-overlay] h2").TextContent, Is.EqualTo("Loading the Bundle"));
            Assert.That(panel.Find("[data-viewer=loading-message]").TextContent, Is.EqualTo("Reading it from fhir.example"));
            Assert.That(panel.FindAll("[data-viewer=loading-indeterminate]"), Has.Count.EqualTo(1));
            Assert.That(panel.FindAll("[data-viewer=cancel-loading]"), Is.Empty);
        });
    }
}
