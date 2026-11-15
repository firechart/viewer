#pragma warning disable IDE0055, CA2211

using System.Globalization;
using System.Reflection;

namespace Flox.Core;

/// <summary>
/// Constants shared by the platform and the applications built on it.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Name of the claim that carries the user's name.
    /// </summary>
    public const string NAME_CLAIM = "email";
    /// <summary>
    /// Name of the OAuth2 subject claim, the user id.
    /// </summary>
    public const string SUB_CLAIM = "sub";

    /// <summary>
    /// Default FHIR release.
    /// </summary>
    public const string DEFAULT_FHIR_RELEASE = "R4";
    /// <summary>
    /// Name of the FHIR R3 core package.
    /// </summary>
    public const string CORE_PACKAGE_R3 = "hl7.fhir.r3.core";
    /// <summary>
    /// Name of the FHIR R4 core package.
    /// </summary>
    public const string CORE_PACKAGE_R4 = "hl7.fhir.r4.core";
    /// <summary>
    /// Name of the FHIR R4B core package.
    /// </summary>
    public const string CORE_PACKAGE_R4B = "hl7.fhir.r4b.core";
    /// <summary>
    /// Name of the FHIR R5 core package.
    /// </summary>
    public const string CORE_PACKAGE_R5 = "hl7.fhir.r5.core";
    /// <summary>
    /// Default FHIR version, the one of the default release.
    /// </summary>
    public const string DEFAULT_FHIR_VERSION = "4.0.1";

    /// <summary>
    /// Format of the address of a package page on Simplifier; the arguments are the package name and version.
    /// </summary>
    public const string PACKAGE_URL_TEMPLATE = "https://simplifier.net/packages/{0}/{1}";

    /// <summary>
    /// The official HL7 package registry. Unlike packages.simplifier.net its package listings include per-version dependencies, which allows resolving dependency trees from manifests without downloading package tarballs.
    /// </summary>
    public const string DEFAULT_PACKAGE_SERVER = "https://packages2.fhir.org/packages";

    /// <summary>
    /// Registry used for packages (and old versions of packages) published directly to Simplifier that never reached the HL7 registry.
    /// </summary>
    public const string FALLBACK_PACKAGE_SERVER = "https://packages.simplifier.net";
    /// <summary>
    /// Address of Firely's public FHIR R4 server.
    /// </summary>
    public const string FIRELY_PUBLIC_SERVER_URL = "https://server.fire.ly/r4";
    /// <summary>
    /// Address of the public HAPI FHIR R4 server.
    /// </summary>
    public const string HAPI_PUBLIC_SERVER_URL = "https://hapi.fhir.org/baseR4";
    /// <summary>
    /// Address of the public SMART Health IT R4 server.
    /// </summary>
    public const string SMART_PUBLIC_SERVER_URL = "https://r4.smarthealthit.org";
    /// <summary>
    /// Address of the SMART launcher R4 server.
    /// </summary>
    public const string SMART_LAUNCHER_SERVER_URL = "https://launch.smarthealthit.org/v/r4/fhir";
    /// <summary>
    /// Name of the claim that carries the user's organization.
    /// </summary>
    public const string ORGANIZATION_CLAIM = "organization";
    /// <summary>
    /// OpenTelemetry service name of the clients.
    /// </summary>
    public const string OTEL_CLIENT_SERVICE_NAME = "flox-client";
    /// <summary>
    /// OpenTelemetry service name of the server.
    /// </summary>
    public const string OTEL_SERVER_SERVICE_NAME = "flox-server";
    /// <summary>
    /// Authentication type of the identities the platform creates itself.
    /// </summary>
    public const string AUTHENTICATION_TYPE = "Flox";

    /// <summary>
    /// The page of a client application that "Back" on the sign-in page of the server leads to: it returns to the page the user signed in from.
    /// </summary>
    public const string SIGN_IN_BACK_PATH = "signin-back";
    /// <summary>
    /// Id of the built-in administrator identity.
    /// </summary>
    public static Guid DEFAULT_ADMIN_ID = new Guid("00000000-0000-0000-0000-000000000001");
    /// <summary>
    /// Id of the built-in service identity.
    /// </summary>
    public static Guid DEFAULT_SERVICE_ID = new Guid("00000000-0000-0000-0000-000000000003");


    /// <summary>
    /// Default organization name.
    /// </summary>
    public static string DEFAULT_ORG_NAME = "flox";
    /// <summary>
    /// Default user name.
    /// </summary>
    public static string DEFAULT_USER_NAME = "flox";
    /// <summary>
    /// The empty Guid as a string, the id of an unknown or anonymous user.
    /// </summary>
    public static string NULL_GUID = Guid.Empty.ToString();
    /// <summary>
    /// Address of the hosted FireChart application.
    /// </summary>
    public const string APP_URL = "https://app.firechart.org";
    /// <summary>
    /// Address of the local machine.
    /// </summary>
    public const string LOCALHOST = "http://localhost";
    /// <summary>
    /// Default size of a page of a list.
    /// </summary>
    public const int DEFAULT_PAGE_SIZE = 20;
    /// <summary>
    /// Default number of items read ahead.
    /// </summary>
    public const int DEFAULT_BUFFER_SIZE = 10;
    /// <summary>
    /// The most items one batch delete may touch.
    /// </summary>
    public const int MAX_BATCH_DELETE = 10000;

    /// <summary>
    /// The documents a client asks the Capabilities of the OData controllers about at a time (a page of a list).
    /// </summary>
    public const int MAX_CAPABILITY_KEYS = 500;

    /// <summary>
    /// The rows of a list read whole in one request (the shared servers of the administration).
    /// </summary>
    public const int MAX_LIST_PAGE = 500;

    /// <summary>
    /// Language of the full-text queries; the full-text indexes of the documents are built with the same one.
    /// </summary>
    public const string FULL_TEXT_CONFIG = "english";

    /// <summary>
    /// Name of the HTTP client that talks to the application server.
    /// </summary>
    public const string APP_HTTP_CLIENT_ID = "app";

    /// <summary>
    /// Names of the FHIR core packages of all supported releases.
    /// </summary>
    public static List<string> CORE_PACKAGES { get; } =
    [
        CORE_PACKAGE_R3,
        CORE_PACKAGE_R4,
        CORE_PACKAGE_R4B,
        CORE_PACKAGE_R5
    ];

    /// <summary>
    /// Name of the hosting environment, from ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT; Development when neither is set.
    /// </summary>
    public static string ENVIRONMENT
    {
        get
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        }
    }

    /// <summary>
    /// Name of the audit database.
    /// </summary>
    public static string AUDIT_DATABASE
    {
        get
        {
            return $"firechart-audit";
        }
    }

    /// <summary>
    /// Name of the setting that holds the Postgres connection string of the environment.
    /// </summary>
    public static string POSTGRES_CONNECTION_STRING_NAME
    {
        get
        {
            return $"POSTGRES_URI_{ENVIRONMENT}".ToUpper(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Name of the Postgres database.
    /// </summary>
    public static string POSTGRES_DATABASE
    {
        get
        {
            return $"firechart";
        }
    }

    /// <summary>
    /// Version of the assembly, four numbers.
    /// </summary>
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

    /// <summary>
    /// Name of the product of the entry assembly; Flox when it has none.
    /// </summary>
    public static string Product
    {
        get
        {
            return Assembly.GetEntryAssembly()
                          ?.GetCustomAttribute<AssemblyProductAttribute>()
                          ?.Product ?? "Flox";
        }
    }

    /// <summary>
    /// Informational version of the assembly, as the build set it.
    /// </summary>
    public static string InformationalVersion
    {
        get
        {
            return typeof(Constants).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                   ?.InformationalVersion ?? "0.0.0";
        }
    }
}
