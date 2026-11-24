using Flox.Shared.Components;
using Bunit;
using FireChart.Viewer.Components;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Pages;

namespace FireChart.Viewer.Tests.Viewer;

// How a summary loads: the page size of a search (the form, else the option, else 20, within the range) and the panel shown while everything loads
public class SummaryLoadingTests
{
    [Test]
    public void ThePageSizeOfASearch_IsTheForms_ElseTheOption_ElseTheDefault_WithinTheRange()
    {
        var own = new SummaryFormSearch { ResourceType = "Observation", PageSize = 5 };
        var none = new SummaryFormSearch { ResourceType = "Condition" };

        Assert.Multiple(() =>
        {
            Assert.That(new SectionSearch(own, "en", pageSize: 100).PageSize, Is.EqualTo(5));
            Assert.That(new SectionSearch(none, "en", pageSize: new ViewerSummaryOptions { PageSize = 100 }.EffectivePageSize()).PageSize, Is.EqualTo(100));
            Assert.That(new SectionSearch(none, "en").PageSize, Is.EqualTo(SectionSearch.DefaultPageSize));
            Assert.That(new ViewerSummaryOptions { PageSize = 5000 }.EffectivePageSize(), Is.EqualTo(ViewerSummaryOptions.MaxPageSize));
        });
    }

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
}
