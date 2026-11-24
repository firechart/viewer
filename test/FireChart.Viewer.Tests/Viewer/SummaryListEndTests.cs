using Bunit;
using FireChart.Viewer.Components;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.Tests.Viewer;

// A list that reads its records page by page is told when its last row comes into view, once for each length; a list without OnEnd asks for nothing
public class SummaryListEndTests
{
    [Test]
    public async Task TheEnd_IsToldOnce_ForEachLengthOfTheList()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var told = 0;
        RenderFragment<string> row = text => builder => builder.AddContent(0, text);

        var list = context.Render<SummaryList<string>>(parameters => parameters.Add(l => l.Rows, ["a", "b", "c"]).Add(l => l.Row, row)
                                                                               .Add(l => l.OnEnd, () => told++));
        list.Render();
        var once = told;
        list.Render(parameters => parameters.Add(l => l.Rows, ["a", "b", "c", "d", "e"]));

        Assert.Multiple(() =>
        {
            Assert.That(once, Is.EqualTo(1), "three rows are all in view; drawing again does not ask again");
            Assert.That(told, Is.EqualTo(2), "a longer list in view asks for the next page");
        });
    }
}
