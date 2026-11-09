namespace Flox.Core.Models;

public static class BackgroundTaskTypes
{
    public const string Unspecified = "Unspecified";

    public const string Simple = "Simple";

    public const string ImportPackage = "ImportPackage";

    // Indexes a raw FHIR package into the fhir_index Postgres schema
    public const string IndexFhirPackagePostgres = "IndexFhirPackagePostgres";
}
