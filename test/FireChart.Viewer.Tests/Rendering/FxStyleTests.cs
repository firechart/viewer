using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The style of the visualization: the vendor fx.css, the customer's over it, and what the customer's may contain
public class FxStyleTests
{
    [Test]
    public void TheOlderToneDanger_UsesTheVariablesOfTheBadgeThatDaisyBuilds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestLibrary.Styles.Daisy, Does.Contain(".fx-badge-error{--fx-badge-color:var(--color-error)"), "the variable carries the prefix of the kit");
            Assert.That(TestLibrary.Styles.Vendor, Does.Contain(".fx-badge-danger").And.Contain("--fx-badge-color: var(--color-error)"));
        });
    }

    [Test]
    public void TheDaisyComponents_ArePrefixed_ScopedToTheirClasses_AndPartOfTheVendorLayer()
    {
        var composed = TestLibrary.Styles.Compose(null);

        Assert.Multiple(() =>
        {
            Assert.That(StyleChecks.CheckVendor(TestLibrary.Styles.Daisy), Is.Null, "the built file passes the checks of the vendor style");
            Assert.That(TestLibrary.Styles.Daisy, Does.Contain(".fx-badge").And.Contain(".fx-list-row").And.Contain(".fx-stat"), "the components of the subset");
            Assert.That(TestLibrary.Styles.Daisy, Does.Not.Contain(":root").And.Not.Contain("[data-theme"), "no rule reaches outside the classes of the kit");
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(TestLibrary.Styles.Daisy, @"(?<![-.\w])(html|body)\s*\{"), Is.False, "no rule of the page itself");
            Assert.That(composed.IndexOf(".fx-badge", StringComparison.Ordinal), Is.LessThan(composed.IndexOf(".fx-banner", StringComparison.Ordinal)), "fx.css comes after the components and wins");
        });
    }

    [Test]
    public void TheVendorStyle_IsEmbedded_AndDefinesTheKit()
    {
        Assert.That(TestLibrary.Styles.Vendor, Does.Contain(".fx-banner").And.Contain(".fx-section").And.Contain(".fx-row").And.Contain(".fx-badge"));
        Assert.That(TestLibrary.Styles.Tokens().Select(token => token.Name), Does.Contain("--fx-color-accent").And.Contain("--fx-gap"));
        Assert.That(StyleChecks.CheckVendor(TestLibrary.Styles.Vendor), Is.Null, "the vendor style passes its own checks");
    }

    [Test]
    public void TheCustomStyle_IsInALaterLayer_SoItWins()
    {
        var text = TestLibrary.Styles.Compose(".fx-root { --fx-gap: 1rem; }");

        Assert.That(text, Does.StartWith("@layer fx, fx-custom;"));
        Assert.That(text.IndexOf("@layer fx-custom {", StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("@layer fx {", StringComparison.Ordinal)));
        Assert.That(text, Does.Contain("--fx-gap: 1rem;"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n ")]
    public void NoCustomStyle_IsTheVendorOneAlone(string? custom)
    {
        Assert.That(TestLibrary.Styles.Compose(custom), Does.Not.Contain("@layer fx-custom {"));
    }

    [TestCase(".fx-root { --fx-color-accent: #0a7; }")]
    [TestCase("/* a comment with url(http://x) and } */ .fx-badge { color: red; }")]
    [TestCase("@media (max-width: 40rem) { .fx-field { display: block; } }")]
    [TestCase(".fx-banner { background-image: url(\"data:image/svg+xml;utf8,<svg/>\"); }")]
    [TestCase(".fx-banner::after { content: \"url(http://x) } {\"; }")]
    public void ACorrectStyle_IsAccepted(string css)
    {
        Assert.That(StyleChecks.Check(css), Is.Null);
    }

    [TestCase("@import url(a.css);")]
    [TestCase("@IMPORT 'a.css';")]
    [TestCase(".a { background: url(https://tracker.example/p.gif); }")]
    [TestCase(".a { background: url( 'https://tracker.example/p.gif' ); }")]
    [TestCase(".a { background: image-set('x.png' 1x); }")]
    [TestCase(".a { width: expression(alert(1)); }")]
    [TestCase(".a { background: url(javascript:alert(1)); }")]
    [TestCase(".a { -moz-binding: url(x); }")]
    [TestCase(".fx-notice { display: none; }")]
    [TestCase(".fx-root .fx-notice-danger { visibility: hidden; }")]
    [TestCase("details.fx-other { display: none; }")]
    [TestCase(".a { content: ''; } </style><script>alert(1)</script>")]
    [TestCase(".\\75rl { }")]
    public void AStyleThatReachesOutOrCarriesCode_IsRefused(string css)
    {
        Assert.That(StyleChecks.Check(css), Is.Not.Null);
    }

    [TestCase(".a { color: red; } } .b { color: blue; ")]
    [TestCase(".a { color: red;")]
    [TestCase("} body { display: none; } @layer x {")]
    [TestCase("/* not closed")]
    [TestCase(".a { content: 'not closed; }")]
    public void AStyleThatCouldLeaveItsLayer_IsRefused(string css)
    {
        Assert.That(StyleChecks.Check(css), Is.Not.Null);
    }

    [Test]
    public void ATooLongStyle_IsRefused()
    {
        Assert.That(StyleChecks.Check(new string(' ', StyleChecks.MaxLength + 1)), Is.Not.Null);
    }
}
