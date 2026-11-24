using System.Globalization;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.SearchForms;
using FireChart.Viewer.Tests.Reports;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireChart.Viewer.Tests.Viewer;

// The search form of a type is the component {Type}.search: one file for every release over the generated form, its texts keys of the resources
public class SearchFormComponentsTests
{
    private static SearchFormDefinition? Read(string xml, string release, IReadOnlyDictionary<string, string>? texts = null)
    {
        return SearchFormComponents.Read(ReportXml.Parse(xml), release, ReportStrings.Of(texts is null ? [] : [texts]), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> ProblemsOf(string body)
    {
        var component = ReportXml.Parse($"""<Component name="Encounter.search"><SearchForm>{body}</SearchForm></Component>""");
        return ReportCheck.Check(component, _ => null, new HashSet<string> { "Subject" }).Select(problem => problem.Message);
    }

    [Test]
    public void TheFiltersOfTheForm_AreShownInTheirOrder_OverTheGeneratedFields_AndTheOtherParametersAreHidden()
    {
        var form = Read("""
            <Component name="Encounter.search">
              <SearchForm title="{{%strings.Find}}">
                <Filter param="subject" label="{{%strings.Subject}}" targets="Patient" lookup="name"/>
                <Group label="More" collapsed="true">
                  <Filter param="class" control="select" releases="r4 r4b"/>
                  <Filter param="subject-status"/>
                </Group>
                <Filter param="subject:Patient.birthdate" label="Born"/>
                <Filter param="x-ward" custom="true" type="token" hidden="true" default="7"/>
              </SearchForm>
            </Component>
            """, "r4", new Dictionary<string, string> { ["Find"] = "Find encounters", ["Subject"] = "Patient" })!;

        var shown = form.Fields.Where(field => !field.Hidden).ToList();
        var subject = form.Fields[0];
        Assert.Multiple(() =>
        {
            Assert.That(form.Title.Resolve(null), Is.EqualTo("Find encounters"));
            Assert.That(shown.Select(field => field.QueryName), Is.EqualTo(new[] { "subject", "class", "subject:Patient.birthdate" }),
                        "subject-status is a parameter of R5 only");
            Assert.That(subject.Label.Resolve(null), Is.EqualTo("Patient"));
            Assert.That(subject.Lookup!.TargetTypes, Is.EqualTo(new[] { "Patient" }));
            Assert.That(subject.Lookup.LookupParameter, Is.EqualTo("name"));
            Assert.That(form.Fields[1] is { Control: SearchFieldControl.Select, GroupCode: "group1" }, Is.True);
            Assert.That(form.Groups.Single() is { CollapsedByDefault: true }, Is.True);
            Assert.That(form.Fields[2] is { Origin: SearchFieldOrigin.Chained, ParameterType: SearchParameterType.Date, Control: SearchFieldControl.DateRange }, Is.True);
            Assert.That(form.Fields[3] is { Origin: SearchFieldOrigin.Custom, ParameterType: SearchParameterType.Token, Hidden: true, DefaultValue: "7" }, Is.True,
                        "a hidden field with a value is a fixed filter");
            Assert.That(form.Fields.Skip(4).All(field => field.Hidden) && form.Fields.Any(field => field.QueryName == "date"), Is.True,
                        "the parameters the form does not name are hidden fields");
        });
    }

    [Test]
    public void OneFile_ServesEveryRelease()
    {
        const string xml = """
            <Component name="Encounter.search">
              <SearchForm><Filter param="class" releases="r4 r4b"/><Filter param="subject-status"/></SearchForm>
            </Component>
            """;

        Assert.Multiple(() =>
        {
            Assert.That(Read(xml, "r5")!.Fields.Where(field => !field.Hidden).Select(field => field.QueryName), Is.EqualTo(new[] { "subject-status" }));
            Assert.That(Read(xml, "r4")!.Fields.Where(field => !field.Hidden).Select(field => field.QueryName), Is.EqualTo(new[] { "class" }));
        });
    }

    [Test]
    public void TheCheck_KnowsTheElementsOfAForm_ItsValues_AndTheParametersOfTheType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProblemsOf("""<Filter param="subject" label="{{%strings.Subject}}"/><Group><Filter param="date"/></Group>"""), Is.Empty);
            Assert.That(ProblemsOf("""<Filter param="nonsense"/>"""), Has.One.Contains("has no search parameter nonsense"));
            Assert.That(ProblemsOf("""<Filter param="nonsense" custom="true"/>"""), Is.Empty, "a parameter of a server");
            Assert.That(ProblemsOf("""<Filter param="subject:Patient.nonsense"/>"""), Has.One.Contains("has no search parameter"));
            Assert.That(ProblemsOf("""<Filter param="date" control="slider" colour="red"/>"""),
                        Is.EquivalentTo(new[] { "the control \"slider\" is none of text, number, numberRange, boolean, date, dateRange, select, multiSelect, autocomplete, reference, quantity, identifier" }
                                        .Append("<Filter> has no attribute colour (it takes param, label, hint, placeholder, control, type, custom, releases, hidden, required, default, valueSet, valueSetVersion, freeText, or, and, comparators, comparator, modifiers, modifier, minLength, maxLength, min, max, pattern, units, unit, targets, lookup, span)")));
            Assert.That(ProblemsOf("""<Filter param="date" label="{{%context.id}}"/>"""), Has.One.Contains("a search form has no data"));
            Assert.That(ProblemsOf("""<Filter param="date" label="{{%strings.Nope}}"/>"""), Has.One.Contains("%strings.Nope is not a key"));
            Assert.That(ProblemsOf("""<Filter param="date"/><Filter param="date"/>"""), Has.One.Contains("a filter twice"));
            Assert.That(ProblemsOf("""<Filter param="date" releases="r6" span="13"/>"""), Has.Exactly(2).Items);
            Assert.That(ProblemsOf("""<Value value="x"/>"""), Has.One.Contains("holds only"));
        });
    }

    [Test]
    public async Task TheFormOfTheLibrary_IsItsComponent_AndAFormThatFailsItsCheck_IsTheGeneratedOne()
    {
        var broken = """<Component name="Encounter.search"><SearchForm><Filter param="nonsense"/></SearchForm></Component>""";
        var assets = new MergedAssets(TestLibrary.Assets, new Dictionary<string, string> { ["Encounter.search"] = broken });
        var forms = new ViewerSearchForms(assets, NullLogger<ViewerSearchForms>.Instance);

        var patient = await forms.FindAsync(new SearchFormKey("r4", "Patient"));
        var encounter = await forms.FindAsync(new SearchFormKey("r4", "Encounter"));
        var observation = await forms.FindAsync(new SearchFormKey("r5", "Observation"));

        Assert.Multiple(() =>
        {
            Assert.That(patient!.Name, Is.EqualTo("Patient.search"));
            Assert.That(patient.Fields[0].QueryName, Is.EqualTo("name"));
            Assert.That(encounter!.IsGenerated, Is.True);
            Assert.That(observation!.IsGenerated, Is.True, "a type without a form");
        });
    }
}
