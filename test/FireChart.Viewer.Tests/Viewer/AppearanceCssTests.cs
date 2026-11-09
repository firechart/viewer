using FireChart.Viewer.Appearance;

namespace FireChart.Viewer.Tests.Viewer;

// The appearance sets the tokens of the document (FloxWind, Radzen, fx-*): a value that could do more than style is left out with an error
public class AppearanceCssTests
{
    [Test]
    public void NoAppearance_IsNoCss()
    {
        Assert.That(AppearanceCss.Compose(null).Css, Is.Empty);
    }

    [Test]
    public void AColour_SetsTheRadzenVariable_AndItsVariants_AboveThePaletteOfFloxWind()
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Variables = { ["colorPrimary"] = "#0f766e", ["colorText"] = "rgb(20, 20, 20)", ["borderRadius"] = "4px" } });

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors, Is.Empty);
            Assert.That(sheet.Css, Does.StartWith(":root:root:root {"), "outranks html:root[data-theme] of FloxWind");
            Assert.That(sheet.Css, Does.Contain("--rz-primary: #0f766e;").And.Contain("--rz-primary-dark: color-mix(in oklab, #0f766e, black 15%);"));
            Assert.That(sheet.Css, Does.Contain("--rz-link-color:"));
            Assert.That(sheet.Css, Does.Contain("--rz-text-color: rgb(20, 20, 20);").And.Contain("--rz-text-title-color: rgb(20, 20, 20);"));
            Assert.That(sheet.Css, Does.Contain("--fx-radius: 4px;"));
        });
    }

    [Test]
    public void ARule_OfAPublicSelector_IsWrittenOnItsClass_InTheLayerOfTheCustomer()
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance
        {
            Rules = { [".Tile:hover"] = new Dictionary<string, string> { ["backgroundColor"] = "#f0fdfa", ["letterSpacing"] = "0.02em" } }
        });

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors, Is.Empty);
            Assert.That(sheet.Css, Does.Contain("@layer viewer-custom {").And.Contain(".fx-tile:hover {").And.Contain("background-color: #f0fdfa;").And.Contain("letter-spacing: 0.02em;"));
        });
    }

    [TestCase("colorPrimary", "url(https://evil.example.test/x.png)")]
    [TestCase("colorPrimary", "red; } body { display: none")]
    [TestCase("fontFamily", "Inter; } * { color: red")]
    public void AValueThatCouldDoMoreThanStyle_IsLeftOut(string name, string value)
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Variables = { [name] = value } });

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Css, Is.Empty);
            Assert.That(sheet.Errors.Single().Code, Is.EqualTo("invalid_value"));
            Assert.That(sheet.Errors.Single().Param, Is.EqualTo($"appearance.variables.{name}"));
        });
    }

    [Test]
    public void AnUnknownName_ASelectorAndAPropertyNotAllowed_AreLeftOutWithTheirPath()
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance
        {
            Variables = { ["colorPrimery"] = "red" },
            Rules =
            {
                [".Sidebar"] = new Dictionary<string, string> { ["color"] = "red" },
                [".Tile"] = new Dictionary<string, string> { ["position"] = "fixed" }
            }
        });

        Assert.That(sheet.Errors.Select(error => error.Param), Is.EquivalentTo(new[] { "appearance.variables.colorPrimery", "appearance.rules..Sidebar", "appearance.rules..Tile.position" }));
    }

    [Test]
    public void AFont_NeedsAnHttpsUrl_AndAFreeStylesheetMayNotCloseTheStyleElement()
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance
        {
            Fonts = [new ViewerFont("Source Sans 3", "https://cdn.example.test/s3.woff2", "200 900"), new ViewerFont("Bad", "http://cdn.example.test/b.woff2")],
            AdvancedStylesheet = "</style><script>alert(1)</script>"
        });

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Css, Does.Contain("@font-face { font-family: \"Source Sans 3\"; src: url(\"https://cdn.example.test/s3.woff2\")"));
            Assert.That(sheet.Css, Does.Not.Contain("script"));
            Assert.That(sheet.Errors.Select(error => error.Param), Is.EquivalentTo(new[] { "appearance.fonts.Bad", "advanced.stylesheet" }));
        });
    }

    [TestCase(ViewerTheme.Inherit, false, null)]
    [TestCase(ViewerTheme.Dark, false, "dark")]
    [TestCase(ViewerTheme.Auto, true, "dark")]
    [TestCase(ViewerTheme.Auto, false, "light")]
    public void TheTheme_IsTheDataThemeOfTheDocument_AndInheritLeavesItToTheHost(ViewerTheme theme, bool prefersDark, string? expected)
    {
        Assert.That(AppearanceCss.DataThemeOf(theme, prefersDark), Is.EqualTo(expected));
    }
}
