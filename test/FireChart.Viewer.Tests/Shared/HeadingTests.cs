using Bunit;
using Flox.Shared.Components;
using Flox.Shared.Layout;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.Tests.Shared;

// The heading of a page: its visible commands, the menu of its rarer ones, and the menu a child puts into its outlet
[TestFixture]
public class HeadingTests
{
    private const string MenuSection = "test-page-menu";

    [Test]
    public async Task TheCommands_TheMenuAndTheMenuOfAChild_StandAfterTheTitle()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();

        var page = context.Render(builder =>
        {
            builder.OpenComponent<Heading>(0);
            builder.AddComponentParameter(1, nameof(Heading.MenuSection), MenuSection);
            builder.AddComponentParameter(2, nameof(Heading.ChildContent), (RenderFragment)(title => title.AddContent(0, "Patient Summary")));
            builder.AddComponentParameter(3, nameof(Heading.Commands), (RenderFragment)(commands =>
            {
                commands.OpenElement(0, "button");
                commands.AddAttribute(1, "data-command", "print");
                commands.CloseElement();
            }));
            builder.AddComponentParameter(4, nameof(Heading.Menu), (RenderFragment)(menu =>
            {
                menu.OpenComponent<PageMenuItem>(0);
                menu.AddComponentParameter(1, nameof(PageMenuItem.Text), "Export");
                menu.CloseComponent();
            }));
            builder.CloseComponent();
            builder.OpenComponent<PageMenu>(5);
            builder.AddComponentParameter(6, nameof(PageMenu.SectionName), MenuSection);
            builder.CloseComponent();
        });

        var heading = page.Find("h2").ParentElement!;
        Assert.Multiple(() =>
        {
            Assert.That(page.Find("h2").TextContent, Is.EqualTo("Patient Summary"));
            Assert.That(heading.QuerySelectorAll("[data-command]"), Has.Count.EqualTo(1));
            Assert.That(heading.QuerySelectorAll("[data-page-menu]"), Has.Count.EqualTo(2), "its own menu and the one of the child in its outlet");
        });
    }

    // The Viewer turns its title into a toolbar from a list to a record: a new outlet of the section beside the old one is refused by Blazor
    [Test]
    public async Task TheToolbar_KeepsTheOutletsOfTheTitle()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var heading = context.Render<Heading>(parameters => parameters
            .Add(component => component.MenuSection, MenuSection)
            .Add(component => component.ChildContent, (RenderFragment)(title => title.AddContent(0, "Patient"))));

        heading.Render(parameters => parameters.Add(component => component.Toolbar, true));
        var toolbar = heading.FindAll("h2").Count;
        heading.Render(parameters => parameters.Add(component => component.Toolbar, false));

        Assert.Multiple(() =>
        {
            Assert.That(toolbar, Is.Zero, "a toolbar has no title");
            Assert.That(heading.Find("h2").TextContent, Is.EqualTo("Patient"));
        });
    }
}
