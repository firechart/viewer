using Hl7.Fhir.Specification;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The FHIR server the Viewer reads: what the readers, the credentials and the caches need of it.
/// </summary>
/// <remarks>
/// A host application maps its own record of a server to it (FireChart's <c>FhirServer</c> implements it). The secrets are never part of it: they are in
/// the <see cref="IServerCredentialStore"/>.
/// </remarks>
/// <example>
/// <code>
/// public sealed record PublicServer(string Key, string Name, string BaseUrl) : IViewerServer
/// {
///     public AuthenticationType AuthenticationType =&gt; AuthenticationType.None;
///     public FhirRelease? FhirRelease =&gt; Hl7.Fhir.Specification.FhirRelease.R4;
///     public int PageSize =&gt; 50;
/// }
///
/// var server = new PublicServer("hapi", "HAPI", "https://hapi.fhir.org/baseR4");
/// </code>
/// </example>
public interface IViewerServer
{
    /// <summary>
    /// A stable key of the server, unique in the application: the caches and the credentials are kept by it together with <see cref="BaseUrl"/>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The name of the server for people (logs, titles).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The base address of the FHIR API (<c>https://hapi.fhir.org/baseR4</c>); a trailing slash does not matter.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// How the requests to the server are authorized.
    /// </summary>
    AuthenticationType AuthenticationType { get; }

    /// <summary>
    /// The release the server is known to speak; used when the server does not answer <c>/metadata</c>. <see langword="null"/> when unknown.
    /// </summary>
    FhirRelease? FhirRelease { get; }

    /// <summary>
    /// How many resources a page of a search asks for (<c>_count</c>), unless the search asks for another number.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// The authority of the OAuth2 sign-in (its <c>.well-known/openid-configuration</c>); <see langword="null"/> for the SMART discovery of the server.
    /// </summary>
    string? Authority => null;

    /// <summary>
    /// The client id of the SMART sign-in (<see cref="AuthenticationType.OAuth2"/>); a public client, no secret.
    /// </summary>
    string? OAuth2ClientId => null;

    /// <summary>
    /// The scope the SMART sign-in asks for (<c>openid fhirUser launch/patient patient/*.rs</c>).
    /// </summary>
    string? OAuth2Scope => null;

    /// <summary>
    /// More parameters of the authorization request, as a query (<c>prompt=login&amp;kc_idp_hint=x</c>).
    /// </summary>
    string? OAuth2ExtraParameters => null;

    /// <summary>
    /// Whether a search asks for the summary of the resources (<c>_summary=true</c>): for a server whose resources are large and whose lists
    /// need only their summary elements (the FHIR facade of a FireChart package: a <c>StructureDefinition</c> without its snapshot).
    /// </summary>
    /// <remarks>
    /// Off by default: the tiles of clinical resources may show elements that are not summary elements. The page of a resource always reads it
    /// whole.
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed record PackageServer(string Key, string Name, string BaseUrl) : IViewerServer
    /// {
    ///     public AuthenticationType AuthenticationType =&gt; AuthenticationType.None;
    ///     public FhirRelease? FhirRelease =&gt; null;
    ///     public int PageSize =&gt; 50;
    ///     public bool SearchSummary =&gt; true;
    /// }
    /// </code>
    /// </example>
    bool SearchSummary => false;
}
