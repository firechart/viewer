using System.Globalization;

namespace FireChart.Viewer.Components;

// A colour for every type of resource: the types of the latest release (R6) and the ones older releases had, in alphabetical order are spread over the rainbow, from red to violet,
// so a type always has the same colour and neighbours in the alphabet differ a little. A type that is not in the table gets no colour.
// The colours are OKLCH with one lightness and one chroma for every hue (the hue alone changes), so none is louder or dimmer than the others. A fixed HSL
// lightness does not do that: its yellow and green are 1.4:1 on white, its blue 3:1 on a dark page. Lightness 0.62 keeps every hue at 3.4:1 or more
// on white and 5:1 or more on a near-black page (WCAG for graphics asks 3:1); chroma 0.10 is inside sRGB for every hue, so the browser does not clip it
/// <summary>
/// A colour for every type of resource of the releases from STU3 to R6, the same for a type everywhere.
/// </summary>
/// <example>
/// <code>
/// var color = ResourceTypeColors.Of("Patient"); // oklch(0.62 0.1 ...)
/// </code>
/// </example>
public static class ResourceTypeColors
{
    // The hues of OKLCH: red is about 25, violet about 305
    private const double FirstHue = 25;
    private const double LastHue = 305;
    private const string Lightness = "0.62";
    private const string Chroma = "0.1";

    // Alphabetical, ordinal; the list changes with a new release of FHIR
    private static readonly string[] s_types =
    [
        "Account", "ActivityDefinition", "ActorDefinition", "AdministrableProductDefinition", "AdverseEvent", "AllergyIntolerance", "Appointment",
        "AppointmentResponse", "ArtifactAssessment", "AuditEvent", "Basic", "Binary", "BiologicallyDerivedProduct",
        "BiologicallyDerivedProductDispense", "BodyStructure", "Bundle", "CapabilityStatement", "CarePlan", "CareTeam", "CatalogEntry", "ChargeItem",
        "ChargeItemDefinition", "Citation", "Claim", "ClaimResponse", "ClinicalAssessment", "ClinicalImpression", "ClinicalUseDefinition",
        "CodeSystem", "Communication", "CommunicationRequest", "CompartmentDefinition", "Composition", "ConceptMap", "Condition",
        "ConditionDefinition", "Consent", "Contract", "Coverage", "CoverageEligibilityRequest", "CoverageEligibilityResponse", "DetectedIssue",
        "Device", "DeviceAlert", "DeviceAssociation", "DeviceDefinition", "DeviceDispense", "DeviceMetric", "DeviceRequest", "DeviceUsage",
        "DeviceUseStatement", "DiagnosticReport", "DocumentManifest", "DocumentReference", "EffectEvidenceSynthesis", "Encounter",
        "EncounterHistory", "Endpoint", "EnrollmentRequest", "EnrollmentResponse", "EpisodeOfCare", "EventDefinition", "Evidence",
        "EvidenceVariable", "ExampleScenario", "ExplanationOfBenefit", "FamilyMemberHistory", "Flag", "FormularyItem", "GenomicStudy", "Goal",
        "GraphDefinition", "Group", "GuidanceResponse", "HealthcareService", "ImagingSelection", "ImagingStudy", "Immunization",
        "ImmunizationEvaluation", "ImmunizationRecommendation", "ImplementationGuide", "Ingredient", "InsurancePlan", "InsuranceProduct",
        "InventoryItem", "InventoryReport", "Invoice", "Library", "Linkage", "List", "Location", "ManufacturedItemDefinition", "Measure",
        "MeasureReport", "Media", "Medication", "MedicationAdministration", "MedicationDispense", "MedicationKnowledge", "MedicationRequest",
        "MedicationStatement", "MedicinalProduct", "MedicinalProductAuthorization", "MedicinalProductContraindication", "MedicinalProductDefinition",
        "MedicinalProductIndication", "MedicinalProductIngredient", "MedicinalProductInteraction", "MedicinalProductManufactured",
        "MedicinalProductPackaged", "MedicinalProductPharmaceutical", "MedicinalProductUndesirableEffect", "MessageDefinition", "MessageHeader",
        "MolecularDefinition", "MolecularSequence", "NamingSystem", "NutritionIntake", "NutritionOrder", "NutritionProduct", "Observation",
        "ObservationDefinition", "OperationDefinition", "OperationOutcome", "Organization", "OrganizationAffiliation", "PackagedProductDefinition",
        "Parameters", "Patient", "PaymentNotice", "PaymentReconciliation", "Permission", "Person", "PersonalRelationship", "PlanDefinition",
        "Practitioner", "PractitionerRole", "Procedure", "Provenance", "Questionnaire", "QuestionnaireResponse", "RegulatedAuthorization",
        "RelatedPerson", "RequestGroup", "RequestOrchestration", "Requirements", "ResearchDefinition", "ResearchElementDefinition", "ResearchStudy",
        "ResearchSubject", "RiskAssessment", "RiskEvidenceSynthesis", "Schedule", "SearchParameter", "ServiceRequest", "Slot", "Specimen",
        "SpecimenDefinition", "StructureDefinition", "StructureMap", "Subscription", "SubscriptionStatus", "SubscriptionTopic", "Substance",
        "SubstanceDefinition", "SubstanceNucleicAcid", "SubstancePolymer", "SubstanceProtein", "SubstanceReferenceInformation",
        "SubstanceSourceMaterial", "SubstanceSpecification", "SupplyDelivery", "SupplyRequest", "Task", "TerminologyCapabilities", "TestPlan",
        "TestReport", "TestScript", "Transport", "ValueSet", "VerificationResult", "VisionPrescription"
    ];

    private static readonly Dictionary<string, string> s_colors = Build();

    /// <summary>
    /// The types of the table, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> Types => s_types;

    /// <summary>
    /// The colour of a type.
    /// </summary>
    /// <param name="resourceType">The type (<c>Patient</c>); the case does not matter.</param>
    /// <returns>A CSS colour, or <see langword="null"/> for a type that is not in the table.</returns>
    public static string? Of(string? resourceType)
    {
        return resourceType is not null && s_colors.TryGetValue(resourceType, out var color) ? color : null;
    }

    private static Dictionary<string, string> Build()
    {
        var result = new Dictionary<string, string>(s_types.Length, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < s_types.Length; index++)
        {
            var hue = FirstHue + (LastHue - FirstHue) * index / (s_types.Length - 1);
            result[s_types[index]] = string.Create(CultureInfo.InvariantCulture, $"oklch({Lightness} {Chroma} {hue:0.#})");
        }

        return result;
    }
}
