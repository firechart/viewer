using Flox.Core.Util;
using Flox.Shared.Extensions;
using Microsoft.Extensions.Configuration;

namespace Flox.Tests.Settings;

public class ClientAppPathTests
{
    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase("/", "")]
    [TestCase("app", "/app")]
    [TestCase("/app", "/app")]
    [TestCase("/app/", "/app")]
    [TestCase(" /tools/app/ ", "/tools/app")]
    public void Normalize_GivesRootOrLeadingSlashWithoutTrailingOne(string? value, string expected)
    {
        Assert.That(ClientAppPath.Normalize(value), Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_IsAppWithoutTheSetting_AndRootWithAnEmptyOne()
    {
        var without = new ConfigurationBuilder().Build();
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ClientAppPath.ConfigurationKey] = "" })
                                              .Build();

        Assert.Multiple(() =>
        {
            Assert.That(ClientAppPath.Resolve(without), Is.EqualTo("/app"));
            Assert.That(ClientAppPath.Resolve(empty), Is.EqualTo(""));
        });
    }

    [TestCase("/", "./")]
    [TestCase("/settings", "settings")]
    [TestCase("/package/a/1.0", "package/a/1.0")]
    [TestCase("/?x=1", "./?x=1")]
    [TestCase("/search?q=a", "search?q=a")]
    [TestCase("~/Admin", "/Admin")]
    [TestCase("settings", "settings")]
    [TestCase("https://example.com/x", "https://example.com/x")]
    [TestCase("//example.com/x", "//example.com/x")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void ToHref_MakesRoutesRelativeToTheBase(string? route, string expected)
    {
        Assert.That(AppUrl.ToHref(route), Is.EqualTo(expected));
    }
}
