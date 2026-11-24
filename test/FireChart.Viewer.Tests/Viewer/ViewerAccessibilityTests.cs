using Bunit;
using FireChart.Viewer.Components;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Pages;
using Microsoft.AspNetCore.Components.Web;

namespace FireChart.Viewer.Tests.Viewer;

// The keyboard and a reader reach what the mouse does: the header of a section is a heading with a toggle button, a row with an address is a link, a
// row that chooses is a toggle button, a result of the lab table has a link and its table a name
public class ViewerAccessibilityTests
{
    private static ResourceSummary Obs(string id)
    {
        return new ResourceSummary(id, "Observation", $$"""{ "resourceType": "Observation", "id": "{{id}}", "status": "final", "code": { "text": "{{id}}" }, "effectiveDateTime": "2020-03-01T08:00:00Z" }""");
    }

    [Test]
    public void TheHeaderOfASection_IsAHeadingWithAButtonThatSaysWhetherTheSectionIsOpen()
    {
        var section = new SummarySection(new SummaryFormSection { Code = "labs" }, [], "en");
        var toggled = 0;
        using var context = new BunitContext();

        var shell = context.Render<SectionShell>(parameters => parameters.Add(component => component.Section, section).Add(component => component.OnToggle, () => toggled++));

        var button = shell.Find("h3 > button.fx-summary-toggle");
        button.Click();
        Assert.Multiple(() =>
        {
            Assert.That(button.GetAttribute("type"), Is.EqualTo("button"));
            Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("true"));
            Assert.That(shell.FindAll("#" + button.GetAttribute("aria-controls")), Has.Count.EqualTo(1), "the button names the body it opens");
            Assert.That(toggled, Is.EqualTo(1));
        });
    }

    [Test]
    public void AResultOfTheLabTable_HasALink_TheTableIsNamedByItsGroup_AnEmptyColumnHasAHiddenName()
    {
        var section = new SummarySection(new SummaryFormSection { Code = "labs" }, [], "en");
        using var context = new BunitContext();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var view = context.Render<FireChart.Viewer.Reports.ReportLab>(parameters => parameters
            .Add(component => component.Section, section)
            .Add(component => component.Groups, [new LabGroup("day:2020-03-01", null, new DateOnly(2020, 3, 1), [Obs("o1")])])
            .Add(component => component.Tile, (ResourceSummary item) => (Microsoft.AspNetCore.Components.RenderFragment)(builder => builder.AddContent(0, item.Id)))
            .Add(component => component.Text, (ResourceSummary _, string cell) => cell == "test" ? "Potassium" : string.Empty)
            .Add(component => component.Flag, (ResourceSummary _) => (Microsoft.AspNetCore.Components.RenderFragment)(_ => { }))
            .Add(component => component.HasFlag, (ResourceSummary _) => false)
            .Add(component => component.HrefOf, (ResourceSummary item) => "resources/Observation/" + item.Id));

        var table = view.Find("table.fx-lab-table");
        Assert.Multiple(() =>
        {
            Assert.That(view.Find("td.fx-lab-test a").GetAttribute("href"), Is.EqualTo("resources/Observation/o1"));
            Assert.That(view.Find("#" + table.GetAttribute("aria-labelledby")).ClassList, Does.Contain("fx-lab-day"));
            Assert.That(table.QuerySelectorAll("th").Select(th => th.TextContent.Trim()), Has.None.Empty, "no blank header");
            Assert.That(table.QuerySelectorAll("th .fx-visually-hidden"), Has.Length.EqualTo(2), "the range and the flag: no row has them");
        });
    }

    // The text of a soft badge (a status, a flag) is not the tone alone: amber or green on its own tint is under 4.5:1
    [Test]
    public void TheTextOfASoftBadge_IsItsToneMixedTowardTheText()
    {
        Assert.That(TestLibrary.Styles.Vendor, Does.Contain("color: color-mix(in oklab, var(--fx-badge-color, var(--fx-color-text)) 50%, var(--fx-color-text));"));
    }
}
