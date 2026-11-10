using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The view of a resource ("view" mode): the banners of the patient and the encounter over the resource, the sections, the links, the safety of the output
public class ViewTemplatesTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "active": true, "birthDate": "1974-12-25", "gender": "male",
          "name": [ { "family": "Chalmers & Co", "given": [ "Peter", "James" ] } ],
          "identifier": [ { "value": "MRN-7", "system": "urn:mrn" } ],
          "telecom": [ { "system": "phone", "value": "555-1", "use": "home" } ],
          "address": [ { "line": [ "534 Erewhon St" ], "city": "PleasantVille", "postalCode": "3999" } ],
          "contact": [ { "relationship": [ { "coding": [ { "code": "N", "display": "Next of kin" } ] } ], "name": { "family": "du Marche", "given": [ "Benedicte" ] } } ] }
        """;

    private const string Encounter = """
        { "resourceType": "Encounter", "id": "e1", "status": "finished",
          "class": { "code": "AMB", "display": "ambulatory" }, "type": [ { "text": "Office visit" } ],
          "period": { "start": "2020-03-01T10:15:30+03:00", "end": "2020-03-01T11:00:00+03:00" },
          "subject": { "reference": "Patient/p1" },
          "reasonCode": [ { "text": "Cough" } ],
          "diagnosis": [ { "condition": { "reference": "Condition/c1", "display": "Bronchitis" }, "use": { "text": "Admission" } } ],
          "participant": [ { "type": [ { "text": "attender" } ], "individual": { "reference": "Practitioner/dr1", "display": "Dr. House" } } ] }
        """;

    private const string EncounterR5 = """
        { "resourceType": "Encounter", "id": "e5", "status": "completed",
          "class": [ { "coding": [ { "display": "ambulatory" } ] } ],
          "actualPeriod": { "start": "2021-01-01" },
          "subject": { "reference": "Patient/p1" },
          "reason": [ { "value": [ { "concept": { "text": "Fever" } } ] } ],
          "participant": [ { "actor": { "reference": "Practitioner/dr1", "display": "Dr. Wilson" } } ] }
        """;

    private const string Observation = """
        { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Blood pressure" },
          "subject": { "reference": "Patient/p1" }, "encounter": { "reference": "Encounter/e1" },
          "effectiveDateTime": "2020-03-01T10:20:00+03:00",
          "component": [ { "code": { "text": "Systolic" }, "valueQuantity": { "value": 150, "unit": "mmHg" }, "interpretation": [ { "coding": [ { "code": "H", "display": "High" } ] } ] } ],
          "note": [ { "text": "<script>alert(1)</script> note" } ] }
        """;

    private const string Condition = """
        { "resourceType": "Condition", "id": "c1", "code": { "text": "Bronchitis" },
          "clinicalStatus": { "coding": [ { "code": "active" } ] }, "severity": { "text": "Moderate" },
          "subject": { "reference": "Patient/p1" }, "onsetDateTime": "2020-02-27" }
        """;

    private const string Procedure = """
        { "resourceType": "Procedure", "id": "pr1", "status": "completed", "code": { "text": "Chest X-ray" },
          "subject": { "reference": "Patient/p1" }, "performedDateTime": "2020-03-01T10:40:00+03:00",
          "performer": [ { "actor": { "reference": "Practitioner/dr1", "display": "Dr. House" } } ] }
        """;

    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    private static ResourcePool Pool(params string[] resources)
    {
        return FhirRenderer.CreatePool(resources.Select(json => new PoolEntry(json)));
    }

    private static async System.Threading.Tasks.Task<string> ViewAsync(string json, ResourcePool? pool = null, Hl7.Fhir.Introspection.ModelInspector? model = null)
    {
        var problems = new List<string>();
        var renderer = new FhirRenderer([s_vendor], problems.Add, FixedClock.Offset(3, new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero)));
        var html = FxHtml.Sanitize(await renderer.RenderAsync(json, "view", model, pool), link => "/app/" + link[FxHtml.FhirScheme.Length..]);
        Assert.That(problems, Is.Empty);
        return html;
    }

    [Test]
    public async System.Threading.Tasks.Task APatient_HasTheBanner_AndTheSections()
    {
        var html = await ViewAsync(Patient);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("""<h2 class="fx-banner-title">Peter James Chalmers &amp; Co</h2>"""), "the name is encoded once, and is no link on its own page");
            Assert.That(html, Does.Contain("<span>1974-12-25 (51 y)</span>").And.Contain("<span>ID MRN-7</span>").And.Contain("<span>male</span>"));
            Assert.That(html, Does.Contain("555-1").And.Contain("534 Erewhon St, PleasantVille, 3999").And.Contain("Benedicte du Marche").And.Contain("Next of kin"));
            Assert.That(html, Does.Not.Contain("unrendered"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ThePatientBanner_FlagsADeceasedAndAnInactivePatient()
    {
        var html = await ViewAsync("""{ "resourceType": "Patient", "id": "p2", "active": false, "deceasedDateTime": "2020-05-06T07:08:09Z" }""");

        Assert.That(html, Does.Contain("""<span class="fx-badge fx-badge-soft fx-badge-sm fx-badge-danger">Deceased</span>""").And.Contain("2020-05-06 10:08").And.Contain("""<span class="fx-badge fx-badge-soft fx-badge-sm fx-badge-warning">Inactive</span>"""));
    }

    [Test]
    public async System.Threading.Tasks.Task AnObservation_StartsWithThePatientAndTheEncounter_FromThePool()
    {
        var html = await ViewAsync(Observation, Pool(Patient, Encounter));

        var patient = html.IndexOf("Peter James Chalmers", StringComparison.Ordinal);
        var encounter = html.IndexOf("Encounter: Office visit", StringComparison.Ordinal);
        var title = html.IndexOf("""<h2 class="fx-title-text">Blood pressure</h2>""", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(patient, Is.GreaterThanOrEqualTo(0));
            Assert.That(encounter, Is.GreaterThan(patient));
            Assert.That(title, Is.GreaterThan(encounter));
            Assert.That(html, Does.Contain("""<a href="/app/Patient/p1">Peter James Chalmers &amp; Co</a>"""), "the banners of another resource lead to their resources");
            Assert.That(html, Does.Contain("""<a href="/app/Encounter/e1">Encounter: Office visit</a>"""));
            Assert.That(FxHtml.PlainText(html), Does.Contain("150 mmHg"));
            Assert.That(html, Does.Contain("""<span class="fx-badge fx-badge-soft fx-badge-sm fx-badge-warning">High</span>"""));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task WhatTheServerWrote_CannotInjectMarkup()
    {
        var html = await ViewAsync(Observation, Pool(Patient, Encounter));

        Assert.That(html, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt; note").And.Not.Contain("<script"));
    }

    [Test]
    public async System.Threading.Tasks.Task WithoutThePatientInThePool_TheBannerSaysWhatTheReferenceSays()
    {
        var html = await ViewAsync("""{ "resourceType": "Observation", "id": "o2", "status": "final", "code": { "text": "Pulse" }, "subject": { "reference": "Patient/p9", "display": "Jane Roe" }, "encounter": { "reference": "Encounter/e9" } }""");

        Assert.That(html, Does.Contain("""<a href="/app/Patient/p9">Jane Roe</a>""").And.Contain("""<a href="/app/Encounter/e9">Encounter/e9</a>"""));
    }

    [Test]
    public async System.Threading.Tasks.Task AnEncounter_HasThePatientBanner_AndItsOwnWithoutALink()
    {
        var html = await ViewAsync(Encounter, Pool(Patient));

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("""<a href="/app/Patient/p1">"""));
            Assert.That(html, Does.Contain("""<h2 class="fx-banner-title">Encounter: Office visit</h2>"""));
            Assert.That(html, Does.Contain("2020-03-01 10:15 - 11:00").And.Contain("Cough").And.Contain("Dr. House").And.Contain("Admission"));
            Assert.That(html, Does.Contain("""<a href="/app/Condition/c1">Bronchitis</a>"""), "a diagnosis that is not at hand is shown by what the reference says");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnEncounterOfR5_ShowsItsReasonAndParticipants()
    {
        var model = await new ReleaseModels(new NoReleaseAssemblyLoader()).GetAsync("R5");

        var html = await ViewAsync(EncounterR5, Pool(Patient), model);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Fever").And.Contain("Dr. Wilson").And.Contain("2021-01-01"));
            Assert.That(html, Does.Contain("""<span class="fx-badge fx-badge-soft fx-badge-sm fx-badge-success">completed</span>"""));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AReasonCodeOfR4_IsNotTakenForTheReasonOfR5()
    {
        var html = await ViewAsync(Encounter, Pool(Patient));

        Assert.That(Count(html, "Cough"), Is.EqualTo(1));
        Assert.That(html, Does.Contain("""<span class="fx-count">2</span>"""));
    }

    [Test]
    public async System.Threading.Tasks.Task TheReasonReferenceOfR4_IsNotTakenForTheReasonOfR5()
    {
        var html = await ViewAsync("""
            { "resourceType": "Procedure", "id": "pr2", "status": "completed", "code": { "text": "Biopsy" },
              "reasonCode": [ { "text": "Lump" } ], "reasonReference": [ { "reference": "Condition/c1", "display": "Mass" } ] }
            """);

        Assert.That(html, Does.Contain("Lump").And.Contain("""<a href="/app/Condition/c1">Mass</a>"""));
        Assert.That(Count(html, "fx-label\">Reason<"), Is.EqualTo(2), "one field for the codes, one for the references; none for a phantom reason");
    }

    [Test]
    public async System.Threading.Tasks.Task ACondition_ShowsItsStatusesAndOnset()
    {
        var html = await ViewAsync(Condition, Pool(Patient));

        Assert.That(html, Does.Contain("""<h2 class="fx-title-text">Bronchitis</h2>""").And.Contain("""<span class="fx-badge fx-badge-soft fx-badge-sm fx-badge-success">active</span>""").And.Contain("Onset 2020-02-27").And.Contain("Moderate"));
    }

    [Test]
    public async System.Threading.Tasks.Task AProcedure_ShowsWhenAndByWhom()
    {
        var html = await ViewAsync(Procedure, Pool(Patient));

        Assert.That(html, Does.Contain("""<h2 class="fx-title-text">Chest X-ray</h2>""").And.Contain("2020-03-01 10:40").And.Contain("""<a href="/app/Practitioner/dr1">Dr. House</a>"""));
    }

    [TestCase("""{ "resourceType": "Observation", "id": "o" }""")]
    [TestCase("""{ "resourceType": "Condition", "id": "c" }""")]
    [TestCase("""{ "resourceType": "Procedure", "id": "p" }""")]
    [TestCase("""{ "resourceType": "Encounter", "id": "e" }""")]
    [TestCase("""{ "resourceType": "Patient", "id": "p" }""")]
    public async System.Threading.Tasks.Task AResourceWithNothingInIt_StillRenders(string json)
    {
        var html = await ViewAsync(json);

        Assert.That(html, Is.Not.Empty);
    }

    [Test]
    public async Task TheViewTemplatesDeclareWhatTheyNeedFromTheServer()
    {
        var renderer = new FhirRenderer([s_vendor]);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await renderer.NeedsForAsync("Observation", "view")).Include, Is.EqualTo(["Observation:subject", "Observation:encounter"]));
            Assert.That((await renderer.NeedsForAsync("Procedure", "view")).Include, Is.EqualTo(["Procedure:subject", "Procedure:encounter"]));
            Assert.That((await renderer.NeedsForAsync("Condition", "view")).Include, Is.EqualTo(["Condition:subject", "Condition:encounter"]));
            Assert.That((await renderer.NeedsForAsync("Encounter", "view")).Include, Is.EqualTo(["Encounter:subject"]));
            Assert.That((await renderer.NeedsForAsync("Patient", "view")).IsEmpty, Is.True);
        });
    }

    [TestCase("1974-12-25", "2026-10-31", "51 y")]
    [TestCase("1974-12-31", "2026-10-31", "51 y")]
    [TestCase("2026-03-15", "2026-10-31", "7 mo")]
    [TestCase("2026-10-20", "2026-10-31", "11 d")]
    [TestCase("1974", "2026-10-31", "52 y")]
    [TestCase("1974-12-25T10:00:00Z", "2026-10-31", "51 y")]
    public void TheAge_IsCountedFromTheDateAsFhirWritesIt(string born, string today, string expected)
    {
        Assert.That(FhirFilters.Age(new Fluid.Values.StringValue(born), DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture)).ToStringValue(), Is.EqualTo(expected));
    }

    [TestCase("2999-01-01")]
    [TestCase("not a date")]
    [TestCase("2020-13-40")]
    public void ADateThatIsNotAPastDate_HasNoAge(string born)
    {
        Assert.That(FhirFilters.Age(new Fluid.Values.StringValue(born), new DateOnly(2026, 10, 31)).IsNil(), Is.True);
    }

    private static int Count(string text, string part)
    {
        return text.Split(part).Length - 1;
    }
}
