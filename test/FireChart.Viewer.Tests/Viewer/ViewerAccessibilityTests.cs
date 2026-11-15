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
    public void ARowWithAnAddress_IsALink_APlainClickStaysInThePage_ACtrlClickOpensAnotherTab()
    {
        var opened = 0;
        using var context = new BunitContext();
        context.JSInterop.SetupVoid("open", "resources/Observation/o1", "_blank", "noopener");

        var tile = context.Render<Tile>(parameters => parameters.Add(component => component.Model, ResourceTiles.Plain(Obs("o1")))
                                                                 .Add(component => component.Href, "resources/Observation/o1")
                                                                 .Add(component => component.OnClick, () => opened++));

        var link = tile.Find("a.fx-summary-row");
        link.Click();
        link.Click(new MouseEventArgs { CtrlKey = true });
        Assert.Multiple(() =>
        {
            Assert.That(link.GetAttribute("href"), Is.EqualTo("resources/Observation/o1"));
            Assert.That(opened, Is.EqualTo(1), "the ctrl-click does not open the record in the page");
            Assert.That(context.JSInterop.VerifyInvoke("open").Arguments[0], Is.EqualTo("resources/Observation/o1"));
            Assert.That(tile.FindAll("ul, li"), Is.Empty, "the row is no list of its own");
        });
    }

    [Test]
    public void ARowThatChooses_IsAToggleButton()
    {
        using var context = new BunitContext();

        var tile = context.Render<Tile>(parameters => parameters.Add(component => component.Model, ResourceTiles.Plain(Obs("o1"))).Add(component => component.Pressed, true));

        var button = tile.Find("button.fx-summary-row");
        Assert.Multiple(() =>
        {
            Assert.That(button.GetAttribute("type"), Is.EqualTo("button"));
            Assert.That(button.GetAttribute("aria-pressed"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void AResultOfTheLabTable_HasALink_TheTableIsNamedByItsGroup_AnEmptyColumnHasAHiddenName()
    {
        var section = new SummarySection(new SummaryFormSection { Code = "labs" }, [], "en");
        section.LabGroups = [new LabGroup("day:2020-03-01", null, new DateOnly(2020, 3, 1), [Obs("o1")])];
        using var context = new BunitContext();

        var view = context.Render<LabResultsView>(parameters => parameters.Add(component => component.Section, section)
                                                                           .Add(component => component.HrefOf, item => "resources/Observation/" + item.Id));

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
        Assert.That(TestLibrary.Styles.Vendor, Does.Contain(".fx-root .fx-badge-soft {\n  color: color-mix(in oklab, var(--fx-badge-color, var(--color-base-content)) 50%, var(--fx-color-text));"));
    }
}
