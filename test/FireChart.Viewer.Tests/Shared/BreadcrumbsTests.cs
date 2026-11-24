using Bunit;
using Flox.Core.Host;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Tests.Shared;

// The crumbs are the pages above this one: each is shown whole, none is the current page
[TestFixture]
public class BreadcrumbsTests
{
    [Test]
    public void APath_ShowsEveryCrumbWithNoCurrentPage()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        IFloxMenuItem[] path = [new FloxMenuItem(href: "/", icon: "home"), new FloxMenuItem("Servers", href: "server"), new FloxMenuItem("HAPI"),
                                new FloxMenuItem("Patient", href: "server/1/resources/Patient")];

        var crumbs = context.Render<Breadcrumbs>(parameters => parameters.Add(component => component.Items, path));

        var items = crumbs.FindAll("li[data-crumb]");
        Assert.Multiple(() =>
        {
            Assert.That(items.Select(item => item.TextContent.Trim()), Is.EqualTo(new[] { "home", "Servers", "HAPI", "Patient" }));
            Assert.That(items.Select(item => item.QuerySelector("a")?.GetAttribute("href")), Is.EqualTo(new[] { "./", "server", null, "server/1/resources/Patient" }));
            Assert.That(crumbs.FindAll("[aria-current]"), Is.Empty);
            // A separator with its chevron between two crumbs, none before the first
            Assert.That(crumbs.FindAll("li[role=presentation]").Select(separator => separator.QuerySelector("svg") is not null), Is.EqualTo(new[] { true, true, true }));
            Assert.That(crumbs.Find("ol > li").HasAttribute("data-crumb"), Is.True);
        });
    }
}
