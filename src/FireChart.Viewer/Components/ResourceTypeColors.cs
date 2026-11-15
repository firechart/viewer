namespace FireChart.Viewer.Components;

// A colour for every type of resource of the releases from STU3 to R6, by what the type is about: twelve groups (people, clinical, medications,
// diagnostics, ...), each one of the system colours of Apple (red, orange, yellow, green, mint, teal, cyan, blue, indigo, purple, pink, brown). The colour
// is a token of fx.css (--fx-type-{colour}): on a light page the variant of Apple's "Increase Contrast" (every one 4.2:1 or more on white), on a dark page
// Apple's dark variant (3.5:1 or more); a theme may set them. A type that is not in the table gets no colour.
/// <summary>
/// A colour for every type of resource of the releases from STU3 to R6, the same for a type everywhere: the colour of its group (people and
/// organizations, clinical, medications, diagnostics, encounters and scheduling, plans and requests, documents, devices, financial, conformance and
/// terminology, research, infrastructure), as a CSS variable of the Viewer's style that follows the light or dark theme.
/// </summary>
/// <remarks>
/// The colours are the system colours of Apple. The variables are <c>--fx-type-red</c>, <c>--fx-type-orange</c>, <c>--fx-type-yellow</c>,
/// <c>--fx-type-green</c>, <c>--fx-type-mint</c>, <c>--fx-type-teal</c>, <c>--fx-type-cyan</c>, <c>--fx-type-blue</c>, <c>--fx-type-indigo</c>,
/// <c>--fx-type-purple</c>, <c>--fx-type-pink</c> and <c>--fx-type-brown</c> of <c>styles/fx.css</c>; a theme of the Viewer may set them.
/// </remarks>
/// <example>
/// <code>
/// var color = ResourceTypeColors.Of("Patient"); // var(--fx-type-blue)
/// </code>
/// </example>
public static class ResourceTypeColors
{
    // Every type in one group; the list changes with a new release of FHIR (ResourceTypeColorsTests keeps it whole)
    private static readonly Group[] s_groups =
    [
        // People and organizations: who the record is about and who acts
        new("blue", "Patient Practitioner PractitionerRole RelatedPerson Person PersonalRelationship Group Organization OrganizationAffiliation Location " +
                 "HealthcareService Endpoint CareTeam VerificationResult"),
        // Clinical: problems, findings about the patient, what was done
        new("red", "Condition AllergyIntolerance AdverseEvent Procedure FamilyMemberHistory ClinicalImpression ClinicalAssessment DetectedIssue " +
                "RiskAssessment Flag BodyStructure BodySite Goal"),
        // Medications, immunizations, products, nutrition
        new("orange", "Medication MedicationAdministration MedicationDispense MedicationKnowledge MedicationRequest MedicationStatement MedicinalProduct " +
                   "MedicinalProductAuthorization MedicinalProductContraindication MedicinalProductDefinition MedicinalProductIndication " +
                   "MedicinalProductIngredient MedicinalProductInteraction MedicinalProductManufactured MedicinalProductPackaged " +
                   "MedicinalProductPharmaceutical MedicinalProductUndesirableEffect Immunization ImmunizationEvaluation ImmunizationRecommendation " +
                   "Substance SubstanceDefinition SubstanceNucleicAcid SubstancePolymer SubstanceProtein SubstanceReferenceInformation " +
                   "SubstanceSourceMaterial SubstanceSpecification Ingredient ManufacturedItemDefinition PackagedProductDefinition " +
                   "AdministrableProductDefinition ClinicalUseDefinition RegulatedAuthorization NutritionIntake NutritionOrder NutritionProduct " +
                   "FormularyItem BiologicallyDerivedProduct BiologicallyDerivedProductDispense VisionPrescription"),
        // Diagnostics: observations, reports, specimens, imaging, genomics
        new("mint", "Observation ObservationDefinition DiagnosticReport Specimen SpecimenDefinition ImagingStudy ImagingSelection Media MolecularSequence " +
                 "MolecularDefinition GenomicStudy ImagingManifest Sequence"),
        // Encounters, scheduling and the flow of work
        new("yellow", "Encounter EncounterHistory EpisodeOfCare Appointment AppointmentResponse Schedule Slot Task Transport Communication " +
                   "CommunicationRequest"),
        // Plans, requests and their definitions, quality measures
        new("purple", "CarePlan ServiceRequest RequestGroup RequestOrchestration PlanDefinition ActivityDefinition EventDefinition Measure MeasureReport " +
                   "Library GuidanceResponse CatalogEntry ProcedureRequest ReferralRequest ServiceDefinition"),
        // Documents, forms, lists and consent
        new("teal", "Composition DocumentReference DocumentManifest List Questionnaire QuestionnaireResponse Consent"),
        // Devices, supply and inventory
        new("cyan", "Device DeviceAlert DeviceAssociation DeviceDefinition DeviceDispense DeviceMetric DeviceRequest DeviceUsage DeviceUseStatement " +
                 "InventoryItem InventoryReport SupplyDelivery SupplyRequest DeviceComponent"),
        // Financial
        new("green", "Account ChargeItem ChargeItemDefinition Claim ClaimResponse Coverage CoverageEligibilityRequest CoverageEligibilityResponse " +
                  "EnrollmentRequest EnrollmentResponse ExplanationOfBenefit InsurancePlan InsuranceProduct Invoice PaymentNotice " +
                  "PaymentReconciliation Contract EligibilityRequest EligibilityResponse ProcessRequest ProcessResponse"),
        // Conformance and terminology: the specifications themselves
        new("indigo", "StructureDefinition StructureMap CapabilityStatement ImplementationGuide SearchParameter OperationDefinition CompartmentDefinition " +
                   "GraphDefinition MessageDefinition NamingSystem CodeSystem ValueSet ConceptMap TerminologyCapabilities ExampleScenario Requirements " +
                   "ActorDefinition ConditionDefinition SubscriptionTopic TestScript TestReport TestPlan DataElement ExpansionProfile"),
        // Research and evidence
        new("pink", "ResearchStudy ResearchSubject ResearchDefinition ResearchElementDefinition Evidence EvidenceVariable EffectEvidenceSynthesis " +
                 "RiskEvidenceSynthesis Citation ArtifactAssessment EvidenceReport"),
        // Infrastructure, messaging and security
        new("brown", "Bundle Binary Basic Parameters OperationOutcome MessageHeader Subscription SubscriptionStatus AuditEvent Provenance Linkage Permission")
    ];

    private static readonly Dictionary<string, string> s_colors = Build();

    private static readonly string[] s_types = [.. s_colors.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The types of the table, in ordinal order.
    /// </summary>
    /// <example>
    /// <code>
    /// var known = ResourceTypeColors.Types.Contains("Patient"); // true
    /// </code>
    /// </example>
    public static IReadOnlyList<string> Types => s_types;

    /// <summary>
    /// The colour of a type: the CSS variable of its group, which the Viewer's style sets for the light and the dark theme.
    /// </summary>
    /// <param name="resourceType">The type (<c>Patient</c>); the case does not matter.</param>
    /// <returns>A CSS colour (<c>var(--fx-type-blue)</c>), or <see langword="null"/> for a type that is not in the table.</returns>
    /// <example>
    /// <code>
    /// var color = ResourceTypeColors.Of("Observation"); // var(--fx-type-mint)
    /// </code>
    /// </example>
    public static string? Of(string? resourceType)
    {
        return resourceType is not null && s_colors.TryGetValue(resourceType, out var color) ? color : null;
    }

    private static Dictionary<string, string> Build()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in s_groups)
        {
            foreach (var type in group.Types.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!result.TryAdd(type, $"var(--fx-type-{group.Color})"))
                {
                    throw new InvalidOperationException($"The resource type {type} is in two groups of colours.");
                }
            }
        }

        return result;
    }

    // A colour of Apple and the types it marks, separated by spaces
    private sealed record Group(string Color, string Types);
}
