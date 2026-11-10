using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The vendor tiles of the most used resources of R4: each type has a row with its name, the facts that matter and the status as a badge
public class TileCatalogTests
{
    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    public sealed record Sample(string Type, string Json, string[] Facts)
    {
        public override string ToString()
        {
            return Type;
        }
    }

    private static Sample S(string type, string body, params string[] facts)
    {
        return new Sample(type, $$"""{ "resourceType": "{{type}}", "id": "x1", {{body}} }""", facts);
    }

    private static readonly Sample[] s_samples =
    [
        S("Practitioner", """ "name": [ { "family": "House", "given": [ "Gregory" ] } ], "gender": "male", "active": false, "qualification": [ { "code": { "text": "MD" } } ], "identifier": [ { "value": "NPI-1" } ] """, "Gregory House", "MD", "NPI-1", "male", "inactive"),
        S("PractitionerRole", """ "practitioner": { "display": "Dr. House" }, "organization": { "display": "Princeton" }, "specialty": [ { "text": "Diagnostics" } ] """, "Dr. House", "Diagnostics", "Princeton"),
        S("Organization", """ "name": "Acme Clinic", "type": [ { "text": "Hospital" } ], "address": [ { "city": "Berlin" } ], "identifier": [ { "value": "ORG-1" } ], "active": false """, "Acme Clinic", "Hospital", "Berlin", "ORG-1", "inactive"),
        S("Location", """ "name": "Ward 3", "status": "active", "type": [ { "text": "Ward" } ], "address": { "city": "Hamburg" } """, "Ward 3", "Ward", "Hamburg", "active"),
        S("RelatedPerson", """ "patient": { "display": "Peter" }, "relationship": [ { "text": "Mother" } ], "name": [ { "family": "Chalmers", "given": [ "Ann" ] } ], "gender": "female" """, "Ann Chalmers", "Mother", "Peter", "female"),
        S("Device", """ "deviceName": [ { "name": "Pacemaker X", "type": "user-friendly-name" } ], "manufacturer": "Medtronic", "modelNumber": "M-9", "status": "active" """, "Pacemaker X", "Medtronic", "M-9", "active"),
        S("AllergyIntolerance", """ "code": { "text": "Penicillin" }, "clinicalStatus": { "coding": [ { "code": "active" } ] }, "criticality": "high", "recordedDate": "2020-03-01", "category": [ "medication" ], "onsetDateTime": "2019-05-02" """, "Penicillin", "2019-05-02", "medication", "active", "high risk"),
        S("MedicationRequest", """ "status": "active", "intent": "order", "priority": "urgent", "medicationCodeableConcept": { "text": "Amoxicillin 500 mg" }, "authoredOn": "2020-03-01", "dosageInstruction": [ { "text": "1 tablet 3x daily" } ] """, "Amoxicillin 500 mg", "1 tablet 3x daily", "2020-03-01", "active", "urgent"),
        S("MedicationStatement", """ "status": "completed", "medicationCodeableConcept": { "text": "Metformin" }, "effectivePeriod": { "start": "2020-01-01" }, "dosage": [ { "text": "500 mg daily" } ] """, "Metformin", "500 mg daily", "2020-01-01", "completed"),
        S("Immunization", """ "status": "completed", "vaccineCode": { "text": "Influenza" }, "occurrenceDateTime": "2020-10-01", "lotNumber": "L123" """, "Influenza", "2020-10-01", "lot L123", "completed"),
        S("DiagnosticReport", """ "status": "final", "code": { "text": "CBC" }, "category": [ { "text": "Hematology" } ], "effectiveDateTime": "2020-03-01" """, "CBC", "Hematology", "2020-03-01", "final"),
        S("Specimen", """ "status": "available", "type": { "text": "Serum" }, "accessionIdentifier": { "value": "ACC-7" }, "collection": { "collectedDateTime": "2020-03-01" } """, "Serum", "2020-03-01", "ACC-7", "available"),
        S("ImagingStudy", """ "status": "available", "modality": [ { "code": "CT", "display": "Computed Tomography" } ], "description": "Chest CT", "started": "2020-03-01", "numberOfSeries": 2, "numberOfInstances": 240 """, "Chest CT", "Computed Tomography", "2020-03-01", "2 series", "240 images", "available"),
        S("CarePlan", """ "status": "active", "intent": "order", "title": "Diabetes plan", "period": { "start": "2020-01-01", "end": "2020-12-31" } """, "Diabetes plan", "2020-01-01 - 2020-12-31", "active", "order"),
        S("Goal", """ "lifecycleStatus": "active", "description": { "text": "HbA1c below 7%" }, "startDate": "2020-01-01", "target": [ { "dueDate": "2020-12-31" } ] """, "HbA1c below 7%", "2020-01-01", "due 2020-12-31", "active"),
        S("ServiceRequest", """ "status": "active", "intent": "order", "priority": "stat", "code": { "text": "MRI brain" }, "authoredOn": "2020-03-01", "requester": { "display": "Dr. House" } """, "MRI brain", "2020-03-01", "Dr. House", "stat"),
        S("DocumentReference", """ "status": "current", "docStatus": "final", "description": "Discharge summary", "date": "2020-03-02T09:00:00Z", "author": [ { "display": "Dr. House" } ] """, "Discharge summary", "2020-03-02", "Dr. House", "current", "final"),
        S("CareTeam", """ "status": "active", "name": "Cardiac team", "period": { "start": "2020-01-01" }, "participant": [ { "member": { "display": "A" } }, { "member": { "display": "B" } } ] """, "Cardiac team", "2 members", "active"),
        S("EpisodeOfCare", """ "status": "active", "type": [ { "text": "Diabetes management" } ], "period": { "start": "2020-01-01" }, "managingOrganization": { "display": "Acme Clinic" } """, "Diabetes management", "Acme Clinic", "active"),
        S("Appointment", """ "status": "booked", "description": "Check-up", "start": "2020-03-05T10:00:00Z", "participant": [ { "status": "accepted" } ] """, "Check-up", "2020-03-05", "1 participant", "booked"),
        S("Communication", """ "status": "completed", "category": [ { "text": "Alert" } ], "sent": "2020-03-01T08:00:00Z", "sender": { "display": "Nurse Joy" } """, "Alert", "2020-03-01", "Nurse Joy", "completed"),
        S("Task", """ "status": "requested", "intent": "order", "priority": "asap", "description": "Call the patient", "authoredOn": "2020-03-01", "owner": { "display": "Nurse Joy" } """, "Call the patient", "Nurse Joy", "requested", "asap"),
        S("Consent", """ "status": "active", "scope": { "text": "Privacy" }, "patient": { "display": "Peter" }, "dateTime": "2020-03-01", "provision": { "type": "permit" } """, "Privacy", "Peter", "2020-03-01", "active", "permit"),
        S("Contract", """ "status": "executed", "title": "Service agreement", "issued": "2020-03-01", "applies": { "start": "2020-03-01", "end": "2021-03-01" } """, "Service agreement", "2020-03-01", "executed"),
        S("Account", """ "status": "active", "name": "Peter's account", "servicePeriod": { "start": "2020-01-01" }, "owner": { "display": "Acme Clinic" } """, "Peter's account", "Acme Clinic", "active"),
        S("Coverage", """ "status": "active", "type": { "text": "Health insurance" }, "subscriberId": "S-77", "payor": [ { "display": "AOK" } ], "period": { "start": "2020-01-01" }, "beneficiary": { "display": "Peter" } """, "Health insurance", "AOK", "S-77", "2020-01-01", "active"),
        S("CoverageEligibilityRequest", """ "status": "active", "purpose": [ "benefits" ], "patient": { "display": "Peter" }, "created": "2020-03-01", "insurer": { "display": "AOK" } """, "2020-03-01", "Eligibility: benefits", "AOK", "active"),
        S("CoverageEligibilityResponse", """ "status": "active", "purpose": [ "benefits" ], "outcome": "complete", "created": "2020-03-01", "insurer": { "display": "AOK" }, "disposition": "Covered" """, "AOK", "Eligibility: benefits", "Covered", "complete"),
        S("Claim", """ "status": "active", "type": { "text": "Professional" }, "use": "claim", "created": "2020-03-01", "provider": { "display": "Acme Clinic" }, "total": { "value": 150.5, "currency": "EUR" } """, "Professional", "2020-03-01", "Acme Clinic", "150.5 EUR", "active"),
        S("ExplanationOfBenefit", """ "status": "active", "type": { "text": "Institutional" }, "use": "claim", "created": "2020-03-01", "insurer": { "display": "AOK" }, "outcome": "complete", "payment": { "amount": { "value": 100, "currency": "EUR" } } """, "Institutional", "AOK", "100 EUR", "complete")
    ];

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task TheTile_IsARow_WithTheFactsThatMatter(Sample sample)
    {
        var problems = new List<string>();

        var tile = await new FhirRenderer([s_vendor], problems.Add, FixedClock.Offset(0)).RenderTileAsync(sample.Json);

        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(tile.Html, Does.StartWith("<li class=\"fx-list-row\">").And.Not.Contain("Template error"));
            foreach (var fact in sample.Facts)
            {
                Assert.That(tile.Text, Does.Contain(fact), sample.Type);
            }
        });
    }

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task AResourceWithNothingButAnId_IsStillARow_WithoutAnError(Sample sample)
    {
        var problems = new List<string>();
        var json = $$"""{ "resourceType": "{{sample.Type}}", "id": "bare-1" }""";

        var tile = await new FhirRenderer([s_vendor], problems.Add, FixedClock.Offset(0)).RenderTileAsync(json);

        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(tile.Text, Does.StartWith("bare-1"), "the id when the record has no name");
            Assert.That(tile.Html, Does.Not.Contain("Template error"));
        });
    }

    // The pieces of the kit: the title falls through its sources, a missing part of the subtitle takes its separator with it, the end is there only with badges
    [Test]
    public async System.Threading.Tasks.Task ATile_TakesTheNextSourceOfTheTitle_AndHasNoStraySeparator()
    {
        var renderer = new FhirRenderer([s_vendor], null, FixedClock.Offset(0));

        var bare = await renderer.RenderTileAsync("""{ "resourceType": "Appointment", "id": "a1", "serviceType": [ { "text": "Physiotherapy" } ], "participant": [ { "status": "accepted" } ] }""");
        var booked = await renderer.RenderTileAsync("""{ "resourceType": "Appointment", "id": "a2", "status": "booked", "description": "Check-up" }""");

        Assert.Multiple(() =>
        {
            Assert.That(bare.Html, Does.Contain("<div class=\"fx-tile-title\">Physiotherapy</div>"), "no description: the service type");
            Assert.That(bare.Html, Does.Contain("<div class=\"fx-tile-subtitle\">1 participant</div>"), "no start: nothing before the next part");
            Assert.That(bare.Html, Does.Not.Contain("fx-tile-end"), "no status: no end");
            Assert.That(booked.Html, Does.Contain("<div class=\"fx-tile-end\"><span class=\"fx-badge"), "the status at the end");
        });
    }

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task TheTextOfAResource_IsItsNameOrItsId(Sample sample)
    {
        var renderer = new FhirRenderer([s_vendor], null, FixedClock.Offset(0));

        var text = await renderer.RenderAsync(sample.Json, "text");
        var bare = await renderer.RenderAsync($$"""{ "resourceType": "{{sample.Type}}", "id": "bare-1" }""", "text");

        Assert.Multiple(() =>
        {
            Assert.That(text.Trim(), Is.Not.Empty);
            Assert.That(bare.Trim(), Is.EqualTo("bare-1").Or.EqualTo(sample.Type), "a record without a name is shown by its id");
        });
    }

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task TheView_IsAPageOfTheRecord_WithItsFirstFacts_AndNoErrorOfATemplate(Sample sample)
    {
        var html = await new FhirRenderer([s_vendor], null, FixedClock.Offset(0)).RenderAsync(sample.Json, "view");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("Template error"));
            Assert.That(html, Does.Contain("fx-title"));
            Assert.That(System.Net.WebUtility.HtmlDecode(html), Does.Contain(sample.Facts[0]), sample.Type);
        });
    }

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task TheViewOfABareResource_DoesNotFail(Sample sample)
    {
        var html = await new FhirRenderer([s_vendor], null, FixedClock.Offset(0)).RenderAsync($$"""{ "resourceType": "{{sample.Type}}", "id": "bare-1" }""", "view");

        Assert.That(html, Does.Not.Contain("Template error"));
    }
}
