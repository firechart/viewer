#pragma warning disable IDE0055, CA2211

using System.Globalization;
using System.Reflection;

namespace Flox.Core;

public static class Constants
{
    public const string NAME_CLAIM = "email";
    public const string SUB_CLAIM = "sub";

    public const string DEFAULT_FHIR_RELEASE = "R4";
    public const string CORE_PACKAGE_R3 = "hl7.fhir.r3.core";
    public const string CORE_PACKAGE_R4 = "hl7.fhir.r4.core";
    public const string CORE_PACKAGE_R4B = "hl7.fhir.r4b.core";
    public const string CORE_PACKAGE_R5 = "hl7.fhir.r5.core";
    public const string DEFAULT_FHIR_VERSION = "4.0.1";

    public const string PACKAGE_URL_TEMPLATE = "https://simplifier.net/packages/{0}/{1}";

    // The official HL7 package registry. Unlike packages.simplifier.net its package
    // listings include per-version dependencies, which allows resolving dependency
    // trees from manifests without downloading package tarballs
    public const string DEFAULT_PACKAGE_SERVER = "https://packages2.fhir.org/packages";

    // Some packages (and old versions of packages) were published directly to Simplifier
    // and never reached the HL7 registry
    public const string FALLBACK_PACKAGE_SERVER = "https://packages.simplifier.net";
    public const string FIRELY_PUBLIC_SERVER_URL = "https://server.fire.ly/r4";
    public const string HAPI_PUBLIC_SERVER_URL = "https://hapi.fhir.org/baseR4";
    public const string SMART_PUBLIC_SERVER_URL = "https://r4.smarthealthit.org";
    public const string SMART_LAUNCHER_SERVER_URL = "https://launch.smarthealthit.org/v/r4/fhir";
    public const string ORGANIZATION_CLAIM = "organization";
    public const string OTEL_CLIENT_SERVICE_NAME = "flox-client";
    public const string OTEL_SERVER_SERVICE_NAME = "flox-server";
    public const string AUTHENTICATION_TYPE = "Flox";

    // The page of a client application that "Back" on the sign-in page of the server leads to: it returns to the page the user signed in from
    public const string SIGN_IN_BACK_PATH = "signin-back";
    public static Guid DEFAULT_ADMIN_ID = new Guid("00000000-0000-0000-0000-000000000001");
    public static Guid DEFAULT_SERVICE_ID = new Guid("00000000-0000-0000-0000-000000000003");


    public static string DEFAULT_ORG_NAME = "flox";
    public static string DEFAULT_USER_NAME = "flox";
    public static string NULL_GUID = Guid.Empty.ToString();
    public const string APP_URL = "https://app.firechart.org";
    public const string LOCALHOST = "http://localhost";
    public const int DEFAULT_PAGE_SIZE = 20;
    public const int DEFAULT_BUFFER_SIZE = 10;
    public const int MAX_BATCH_DELETE = 10000;

    // The documents a client asks the Capabilities of the OData controllers about at a time (a page of a list)
    public const int MAX_CAPABILITY_KEYS = 500;

    // Replication: documents in a pull page and in a push, and how far back a pull cursor reaches behind the last document it saw
    public const int MAX_REPLICA_PULL = 500;
    public const int MAX_REPLICA_PUSH = 100;
    public const int REPLICA_CURSOR_OVERLAP_SECONDS = 5;

    // Language of the full-text queries; the full-text indexes of the documents are built with the same one
    public const string FULL_TEXT_CONFIG = "english";

    public const string APP_HTTP_CLIENT_ID = "app";

    public static List<string> CORE_PACKAGES { get; } =
    [
        CORE_PACKAGE_R3,
        CORE_PACKAGE_R4,
        CORE_PACKAGE_R4B,
        CORE_PACKAGE_R5
    ];

    public static string ENVIRONMENT
    {
        get
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        }
    }

    public static string AUDIT_DATABASE
    {
        get
        {
            return $"firechart-audit";
        }
    }

    public static string POSTGRES_CONNECTION_STRING_NAME
    {
        get
        {
            return $"POSTGRES_URI_{ENVIRONMENT}".ToUpper(CultureInfo.InvariantCulture);
        }
    }

    public static string POSTGRES_DATABASE
    {
        get
        {
            return $"firechart";
        }
    }

    public static string AssemblyVersion
    {
        get
        {
            return typeof(Constants).Assembly
                                    .GetName()
                                    .Version
                                   ?.ToString() ?? "0.0.0.0";
        }
    }

    public static string Product
    {
        get
        {
            return Assembly.GetEntryAssembly()
                          ?.GetCustomAttribute<AssemblyProductAttribute>()
                          ?.Product ?? "Flox";
        }
    }

    public static string InformationalVersion
    {
        get
        {
            return typeof(Constants).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                   ?.InformationalVersion ?? "0.0.0";
        }
    }
}
