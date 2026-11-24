using System.Globalization;
using FireChart.Viewer.Core.Reports;

namespace FireChart.Viewer.Tests.Reports;

// The texts of the components are .resx files of .NET (locales/strings.resx, strings.de.resx): a component names a key, with arguments for {0}, {1};
// a key no file has is a problem of the check, as an unknown identifier is to a compiler
public class ReportStringsTests
{
    private const string Resx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <data name="ProblemList" xml:space="preserve"><value>Problem list</value><comment>The title of a section</comment></data>
          <data name="Pair" xml:space="preserve"><value>{0} of {1}</value></data>
          <data name="Logo" type="System.Resources.ResXFileRef, System.Windows.Forms"><value>logo.png</value></data>
        </root>
        """;

    [Test]
    public void AResxFile_GivesItsStrings_AndAFileOfACultureGoesOverItsParents()
    {
        var english = ReportStrings.ReadResx(Resx);
        var german = new Dictionary<string, string> { ["ProblemList"] = "Problemliste" };
        var strings = ReportStrings.Of([english, german]);
        var culture = CultureInfo.GetCultureInfo("de-AT");

        Assert.Multiple(() =>
        {
            Assert.That(english.Keys, Is.EquivalentTo(new[] { "ProblemList", "Pair" }), "a data of another type is left out");
            Assert.That(ReportStrings.ChainOf(CultureInfo.GetCultureInfo("zh-Hans-CN")), Is.EqualTo(new[] { "", "zh", "zh-Hans", "zh-Hans-CN" }));
            Assert.That(ReportStrings.PathOf("de"), Is.EqualTo("locales/strings.de.resx"));
            Assert.That(strings.Format("ProblemList", [], culture), Is.EqualTo("Problemliste"));
            Assert.That(strings.Format("Pair", ["1", "2"], culture), Is.EqualTo("1 of 2"), "not translated: English");
            Assert.That(strings.Format("Pair", ["1"], culture), Is.EqualTo("{0} of {1}"), "an argument missing: the text as it is");
            Assert.That(strings.Format("Nope", [], culture), Is.EqualTo("Nope"));
            Assert.Throws<InvalidDataException>(() => ReportStrings.ReadResx("<root><data"));
            Assert.That(ReportStrings.ReadResx("﻿" + Resx).Keys, Is.EquivalentTo(english.Keys), "a byte order mark read as text is skipped");
        });
    }

    [Test]
    public void AComponent_NamesATextWithItsArguments_AndAKeyThatIsNotThereIsAProblem()
    {
        var component = ReportXml.Parse("""
            <Component name="C">
              <Section title="{{%strings.ProblemList}}">
                <Text>{{%strings.Pair(iif(%context.active, 'a, b', 'c'), %context.id)}}</Text>
                <Text>{{%strings.Nope}}</Text>
                <Text>{{%strings.ProblemList + 'x'}}</Text>
              </Section>
            </Component>
            """);
        var pair = (ReportString)component.Body[0].Children[0].Children[0].Text!.Parts[0];
        var problems = ReportCheck.Check(component, _ => null, new HashSet<string> { "ProblemList", "Pair" }).Select(problem => problem.Message).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pair.Key, Is.EqualTo("Pair"));
            Assert.That(pair.Arguments.Select(argument => argument.Source), Is.EqualTo(new[] { "iif(%context.active, 'a, b', 'c')", "%context.id" }));
            Assert.That(problems, Has.Exactly(1).Contains("%strings.Nope is not a key of locales/strings.resx"));
            Assert.That(problems, Has.Exactly(1).Contains("stands alone between"), "a text inside an expression");
            Assert.That(ReportCheck.Check(component, _ => null).Select(problem => problem.Message), Has.None.Contains("Nope"),
                        "without keys the texts are not checked");
        });
    }
}
