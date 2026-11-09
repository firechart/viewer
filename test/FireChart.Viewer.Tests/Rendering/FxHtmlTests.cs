using FireChart.Viewer.Core;

namespace FireChart.Viewer.Tests.Rendering;

// What the page lets through of the HTML of a template: the data of a FHIR server comes into it, so it is not trusted
public class FxHtmlTests
{
    private static string Clean(string html)
    {
        return FxHtml.Sanitize(html, link => "/app/" + link[FxHtml.FhirScheme.Length..]);
    }

    [Test]
    public void TheMarkupOfTheKit_PassesAsItIs()
    {
        const string html = """<section class="fx-section"><h3 class="fx-section-title">Title <span class="fx-count">2</span></h3><div class="fx-row"><span class="fx-row-main">A &amp; B</span><br><hr></div></section>""";

        Assert.That(Clean(html), Is.EqualTo(html));
    }

    [TestCase("<script>alert(1)</script>text", "text")]
    [TestCase("<style>.a{background:url(https://x.test/p.gif)}</style>text", "text")]
    [TestCase("<iframe src=\"https://x.test\"></iframe>text", "text")]
    [TestCase("<img src=\"https://x.test/p.gif\">text", "text")]
    [TestCase("<svg onload=\"alert(1)\"><circle/></svg>text", "text")]
    [TestCase("<button>go</button>text", "text")]
    [TestCase("<!-- <script> -->text", "text")]
    public void ScriptsStylesAndEmbeddedContent_AreDropped(string html, string expected)
    {
        Assert.That(Clean(html), Is.EqualTo(expected));
    }

    [Test]
    public void AttributesOutsideTheWhiteList_AreDropped()
    {
        var result = Clean("""<div class="fx-row other" style="background:url(https://x.test)" onclick="alert(1)" id="x" data-a="b" title="t">x</div>""");

        Assert.That(result, Is.EqualTo("""<div class="fx-row" title="t">x</div>"""));
    }

    [Test]
    public void AClassOfTheApplication_IsNotLetThrough()
    {
        Assert.That(Clean("""<div class="rz-button fx-badge">x</div>"""), Is.EqualTo("""<div class="fx-badge">x</div>"""));
        Assert.That(Clean("""<div class="rz-button">x</div>"""), Is.EqualTo("<div>x</div>"));
    }

    [Test]
    public void ALinkToAResource_BecomesTheAddressOfTheApplication()
    {
        Assert.That(Clean("""<a href="fhir:Patient/p1">x</a>"""), Is.EqualTo("""<a href="/app/Patient/p1">x</a>"""));
    }

    [TestCase("""<a href="fhir:Patient/p1/../../x">x</a>""")]
    [TestCase("""<a href="fhir:Patient">x</a>""")]
    [TestCase("""<a href="javascript:alert(1)">x</a>""")]
    [TestCase("""<a href="JaVaScRiPt:alert(1)">x</a>""")]
    [TestCase("""<a href="data:text/html,x">x</a>""")]
    [TestCase("""<a href="http://x.test/">x</a>""")]
    [TestCase("""<a href="//x.test/">x</a>""")]
    [TestCase("""<a href="/relative">x</a>""")]
    public void AnyOtherLink_LosesItsAddress(string html)
    {
        Assert.That(Clean(html), Is.EqualTo("<a>x</a>"));
    }

    [Test]
    public void AnAddressOnTheWeb_OpensSafelyInAnotherTab()
    {
        Assert.That(Clean("""<a href="https://x.test/a?b=1&amp;c=2">x</a>"""), Is.EqualTo("""<a href="https://x.test/a?b=1&amp;c=2" target="_blank" rel="noopener noreferrer">x</a>"""));
    }

    [Test]
    public void WithoutAResolver_ALinkToAResourceIsDropped()
    {
        Assert.That(FxHtml.Sanitize("""<a href="fhir:Patient/p1">x</a>"""), Is.EqualTo("<a>x</a>"));
    }

    [Test]
    public void AnUnknownTag_IsDroppedWithItsContentKept()
    {
        Assert.That(Clean("<custom-tag>a <b>b</b></custom-tag>"), Is.EqualTo("a <b>b</b>"));
    }

    [Test]
    public void TagsAreBalanced_WhateverTheInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Clean("<div><span>a"), Is.EqualTo("<div><span>a</span></div>"));
            Assert.That(Clean("<div><p>a</div>b"), Is.EqualTo("<div><p>a</p></div>b"));
            Assert.That(Clean("a</div></span>b"), Is.EqualTo("ab"));
        });
    }

    [Test]
    public void ALessThanSign_ThatOpensNoTag_IsText()
    {
        Assert.That(Clean("1 < 2 and <3"), Is.EqualTo("1 &lt; 2 and &lt;3"));
    }

    [TestCase("<div class=\"fx-row")]
    [TestCase("<div ")]
    [TestCase("<script>alert(1)")]
    [TestCase("<!-- never closed")]
    public void AnUnfinishedTag_LeavesNothingExecutable(string html)
    {
        var result = Clean("ok" + html);

        Assert.That(result, Does.StartWith("ok"));
        Assert.That(result, Does.Not.Contain("alert").And.Not.Contain("<script"));
    }

    [Test]
    public void ATableKeepsItsSpans_WithinLimits()
    {
        Assert.That(Clean("""<table><tr><td colspan="2" rowspan="99">a</td><th colspan="3">b</th></tr></table>"""), Is.EqualTo("""<table><tbody><tr><td colspan="2">a</td><th colspan="3">b</th></tr></tbody></table>"""));
    }

    [Test]
    public void ADetailsElement_StaysOpenWhenItWasOpen()
    {
        Assert.That(Clean("<details open><summary>s</summary>x</details>"), Is.EqualTo("<details open><summary>s</summary>x</details>"));
    }

    [Test]
    public void ADeeplyNestedInput_IsLimited()
    {
        var result = Clean(string.Concat(Enumerable.Repeat("<div>", 5000)) + "x");

        Assert.That(result.Count(c => c == '<') / 2, Is.LessThanOrEqualTo(200));
    }
}
