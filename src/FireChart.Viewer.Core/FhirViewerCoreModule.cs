using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Core;

/// <summary>
/// The bootstrap module of <c>FireChart.Viewer.Core</c>: the named HTTP clients of the FHIR servers and of the terminology servers.
/// </summary>
/// <remarks>
/// The services of the Viewer are <c>[FloxService]</c> types the bootstrapper of Flox finds by itself (<see cref="IFhirServerReader"/>,
/// <see cref="IServerCredentialStore"/>, ...). The two clients have no
/// handler of the host application, so no token of the host goes to a FHIR server or to a terminology server.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyBootstrapper : FloxWasmBootstrapper
/// {
///     public MyBootstrapper(IServiceCollection services, IConfigurationBuilder configuration, ILoggingBuilder logging) : base(services, configuration, logging)
///     {
///         AddModule(new FhirViewerCoreModule());
///     }
/// }
/// </code>
/// </example>
public sealed class FhirViewerCoreModule : BootstrapModule
{
    /// <summary>
    /// How long a request to a FHIR server may take when nothing says otherwise; a request of a server takes its own
    /// (<see cref="IViewerServer.TimeoutSeconds"/>, <see cref="FhirServerHttp.TimeoutOf"/>).
    /// </summary>
    public static readonly TimeSpan FhirServerTimeout = TimeSpan.FromSeconds(FhirServerHttp.DefaultTimeoutSeconds);

    /// <summary>
    /// How long a request to a terminology server may take.
    /// </summary>
    public static readonly TimeSpan TerminologyServerTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Adds the HTTP clients <see cref="FhirServerHttp.HttpClientName"/> and <see cref="TerminologyResolver.HttpClientName"/>.
    /// </summary>
    public override void ConfigureServices()
    {
        Services.AddHttpClient(FhirServerHttp.HttpClientName, client => client.Timeout = FhirServerTimeout);
        Services.AddHttpClient(TerminologyResolver.HttpClientName, client => client.Timeout = TerminologyServerTimeout);
    }
}
