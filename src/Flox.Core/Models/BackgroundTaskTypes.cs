namespace Flox.Core.Models;

/// <summary>The kinds of background tasks.</summary>
public static class BackgroundTaskTypes
{
    /// <summary>A task of no particular kind.</summary>
    public const string Unspecified = "Unspecified";

    /// <summary>A simple task.</summary>
    public const string Simple = "Simple";

    /// <summary>Imports a FHIR package.</summary>
    public const string ImportPackage = "ImportPackage";

    /// <summary>Indexes a raw FHIR package into the fhir_index Postgres schema.</summary>
    public const string IndexFhirPackagePostgres = "IndexFhirPackagePostgres";
}
