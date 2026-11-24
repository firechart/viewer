using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The style of the visualization: the vendor fx.css, the customer's over it, and what the customer's may contain
public class FxStyleTests
{
    // The components (badge, list, table, ...) are in the layer kit inside the vendor layer, so a rule of fx.css outside it wins whatever its specificity
    [Test]
    public void TheComponentsOfTheKit_AreInALayerUnderTheRestOfTheVendorStyle()
    {
        var vendor = TestLibrary.Styles.Vendor;
        var kit = vendor.IndexOf("@layer kit {", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(kit, Is.GreaterThan(0));
            Assert.That(vendor.IndexOf(".fx-badge-danger", StringComparison.Ordinal), Is.GreaterThan(kit), "the older name of the tone error is a badge of the kit");
            Assert.That(vendor, Does.Contain(".fx-list .fx-list-row").And.Contain(".fx-table").And.Contain(".fx-stat").And.Contain(".fx-skeleton"));
            Assert.That(vendor, Does.Not.Contain("--color-base").And.Not.Contain("daisy"), "no variable of daisyUI is left");
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
