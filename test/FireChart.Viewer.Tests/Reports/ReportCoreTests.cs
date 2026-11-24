using System.Globalization;
using FireChart.Viewer.Core.Reports;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Tests.Reports;

// The core of the components: an expression over a resource and the variables, and how an element reads
public class ReportCoreTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "active": true,
          "name": [ { "given": ["Anna", "Maria"], "family": "Schmidt" } ],
          "birthDate": "1974-12-25",
          "maritalStatus": { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/v3-MaritalStatus", "code": "M", "display": "Married" } ] } }
        """;

    private const string Observation = """
        { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Weight" },
          "valueQuantity": { "value": 72.5, "unit": "kg" }, "effectiveDateTime": "2026-11-01T09:30:00Z" }
        """;

    private static PocoNode Parse(string json)
    {
        return ReportExpressions.Parse(json, ModelInfo.ModelInspector);
    }

    private static IReadOnlyList<PocoNode> Evaluate(string expression, Dictionary<string, IReadOnlyList<PocoNode>> variables)
    {
        return ReportExpressions.Evaluate(ReportXml.ParseValue($"{{{{{expression}}}}}").Single!, null, variables);
    }

    [Test]
    public void AnExpression_ReadsTheContextAndTheResultsOfASearch_AndAnElementReadsByItsType()
    {
        var variables = new Dictionary<string, IReadOnlyList<PocoNode>>
        {
            [ReportVariables.Context] = [Parse(Patient)],
            ["obs"] = [Parse(Observation), Parse(Observation)],
        };
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Multiple(() =>
        {
            Assert.That(ReportText.Of(Evaluate("%context.name.first()", variables), culture), Is.EqualTo("Anna Maria Schmidt"));
            Assert.That(ReportText.Of(Evaluate("%context.birthDate", variables), culture), Is.EqualTo("25.12.1974"));
            Assert.That(ReportText.Of(Evaluate("%context.maritalStatus", variables), culture), Is.EqualTo("Married"));
            Assert.That(ReportText.Of(Evaluate("%obs.count()", variables), culture), Is.EqualTo("2"));
            Assert.That(ReportText.Of(Evaluate("%obs.first().value", variables), culture), Is.EqualTo("72,5 kg"));
            Assert.That(Evaluate("%context.active", variables).Single().Poco, Is.InstanceOf<FhirBoolean>());
            Assert.That(ReportExpressions.IsTrue(Evaluate("%context.active", variables)), Is.True);
            Assert.That(ReportExpressions.IsTrue(Evaluate("%context.birthDate.empty()", variables)), Is.False, "the answer of a function");
            Assert.That(ReportExpressions.IsTrue(Evaluate("%context.deceased.exists() and %context.deceased != false", variables)), Is.False);
            Assert.That(ReportExpressions.IsTrue(Evaluate("%context.name", variables)), Is.True, "not a boolean: true when not empty");
        });
    }

    [Test]
    public void TheCheck_FindsUnknownBlocksAttributesVariablesAndCycles()
    {
        var components = new Dictionary<string, ComponentDefinition>
        {
            ["Card1"] = ReportXml.Parse("""<Component name="Card1"><Param name="x" required="true"/><Card1b x="{{%x}}"/></Component>"""),
            ["Card1b"] = ReportXml.Parse("""<Component name="Card1b"><Param name="x"/><Card1 x="{{%x}}"/></Component>"""),
        };
        var report = ReportXml.Parse("""
            <Component name="R">
              <Data><Search id="obs" query="Observation?patient={{%context.id}}"/></Data>
              <Section title="A" color="red">
                <Field label="L"><Section/></Field>
                <Value value="{{%nope.name}}"/>
                <Repeat each="{{%obs}}" as="o"><Value value="{{%o.code}}"/></Repeat>
                <Value value="{{%o.code}}"/>
                <Card1/>
                <Unknown/>
              </Section>
            </Component>
            """);

        var problems = ReportCheck.Check(report, components.GetValueOrDefault).Select(problem => problem.Message).ToList();
        var cycle = ReportCheck.Check(components["Card1"], components.GetValueOrDefault).Select(problem => problem.Message).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(problems, Has.Some.Contains("has no attribute color"));
            Assert.That(problems, Has.Some.Contains("<Section> is not allowed in a line of text"));
            Assert.That(problems, Has.Some.Contains("%nope is not known"));
            Assert.That(problems.Count(problem => problem.Contains("%o is not known", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(problems, Has.Some.Contains("<Card1> needs x="));
            Assert.That(problems, Has.Some.Contains("<Unknown> is neither"));
            Assert.That(cycle, Has.Some.Contains("a cycle of components: Card1 → Card1b → Card1"));
        });
    }
}
