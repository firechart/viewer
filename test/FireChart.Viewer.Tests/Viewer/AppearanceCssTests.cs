using System.Text.RegularExpressions;
using FireChart.Viewer.Appearance;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The appearance sets the tokens of the kit (--fx-*); with a style or a variable the base of a theme passes them to Blazor Blueprint around the content. A value that
// could do more than style is left out with an error
public partial class AppearanceCssTests
{
    [GeneratedRegex(@"(--[a-z0-9-]+)\s*:")]
    private static partial Regex Declared();

    [GeneratedRegex(@"var\((--fx-[a-z0-9-]+)")]
    private static partial Regex ReadToken();

    [Test]
    public void NoAppearance_IsNoCss()
    {
        Assert.That(AppearanceCss.Compose(null).Css, Is.Empty);
    }

    [Test]
    public void AVariable_SetsItsToken_AfterTheStyle_AndTheBaseOfTheThemeComesFirst()
    {
        var theme = FxStyle.ReadThemeFolder(TestLibrary.WebRoot(), "fluent");
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Style = "fluent", Variables = { ["colorPrimary"] = "#0f766e", ["borderRadius"] = "4px" } }, theme);

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors, Is.Empty);
            Assert.That(sheet.Css, Does.StartWith("@layer fx, fx-custom;"), "the order of the layers before any page declares one");
            var bridge = sheet.Css.IndexOf("  --primary: var(--fx-color-accent);", StringComparison.Ordinal);
            var style = sheet.Css.IndexOf("--fx-color-accent: #0f6cbd;", StringComparison.Ordinal);
            var variable = sheet.Css.IndexOf("--fx-color-accent: #0f766e;", StringComparison.Ordinal);
            Assert.That(bridge, Is.GreaterThan(0).And.LessThan(style), "the bridge of the base");
            Assert.That(style, Is.LessThan(variable), "the style in its layer, the variable after it outside the layers");
            Assert.That(sheet.Css, Does.Contain(":root:root:root {\n  --fx-color-accent: #0f766e;\n  --fx-radius: 4px;\n}"));
        });
    }

    [Test]
    public void WithoutAStyleOrAVariable_TheBaseOfATheme_IsLeftOut_AndTheViewerKeepsThePaletteOfItsHost()
    {
        var theme = FxStyle.ReadThemeFolder(TestLibrary.WebRoot(), null);
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Theme = ViewerTheme.Dark, Rules = { [".Tile"] = new Dictionary<string, string> { ["color"] = "red" } } }, theme);

        Assert.That(sheet.Css, Does.Not.Contain("--rz-"));
    }

    [TestCase("Fluent", "is not the name of a style")]
    [TestCase("../fx", "is not the name of a style")]
    [TestCase("nope", "There is no style 'nope'")]
    public void AStyleThatIsNotThere_IsLeftOutWithItsPath(string style, string message)
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Style = style }, FxStyle.ReadThemeFolder(TestLibrary.WebRoot(), style));

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors.Single().Param, Is.EqualTo("appearance.style"));
            Assert.That(sheet.Errors.Single().Message, Does.Contain(message));
        });
    }

    [TestCase(":root { --fx-color-accent: red; } .fx-notice { display: none; }")]
    [TestCase(":root { --fx-font: \"X\"; } @font-face { src: url(https://evil.example.test/x.woff2); }")]
    [TestCase(":root { --fx-color-accent: red; } </style><script>alert(1)</script>")]
    public void AStyleThatCouldHideTheNoticesOrReachOutside_IsLeftOut(string text)
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance { Style = "acme" }, new FxThemeSheets(":root:root:root { --primary: var(--fx-color-accent); }", text));

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors.Single().Param, Is.EqualTo("appearance.style"));
            Assert.That(sheet.Css, Does.Not.Contain("--fx-color-accent: red"));
        });
    }

    // The contract: a theme of the vendor sets only the tokens of the kit (never a token of Blazor Blueprint), passes the checks of a customer style, and every
    // token the bridge reads has a value without a theme
    [Test]
    public void TheThemesOfTheVendor_SetOnlyTheTokensOfTheKit_AndTheBridgeReadsOnlyTokensThatExist()
    {
        var root = TestLibrary.WebRoot();
        var tokens = TestLibrary.Styles.Tokens().Select(token => token.Name).ToHashSet(StringComparer.Ordinal);
        var themes = Directory.GetFiles(Path.Combine(root, FxStyle.Folder, FxStyle.ThemesFolder), "*.css");
        var themed = FxStyle.ReadThemeFolder(root, null).Base;

        Assert.Multiple(() =>
        {
            Assert.That(themes.Select(Path.GetFileNameWithoutExtension), Is.SupersetOf(new[] { "fluent", "compact" }));
            foreach (var file in themes)
            {
                var text = File.ReadAllText(file);
                Assert.That(StyleChecks.Check(text), Is.Null, file);
                Assert.That(Declared().Matches(text).Select(match => match.Groups[1].Value).Where(name => !tokens.Contains(name)), Is.Empty, $"{file}: tokens of the kit only");
            }

            Assert.That(StyleChecks.CheckVendor(themed), Is.Null);
            Assert.That(ReadToken().Matches(themed).Select(match => match.Groups[1].Value).Distinct().Where(name => !tokens.Contains(name)), Is.Empty);
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
    public void ARule_OfAPublicSelector_IsWrittenOnItsClass_InTheLayerOfTheCustomer()
    {
        var sheet = AppearanceCss.Compose(new ViewerAppearance
        {
            Rules = { [".Tile:hover"] = new Dictionary<string, string> { ["backgroundColor"] = "#f0fdfa", ["letterSpacing"] = "0.02em" } }
        });

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Errors, Is.Empty);
            Assert.That(sheet.Css, Does.Contain("@layer fx-custom {").And.Contain(".fx-tile:hover {").And.Contain("background-color: #f0fdfa;").And.Contain("letter-spacing: 0.02em;"));
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
