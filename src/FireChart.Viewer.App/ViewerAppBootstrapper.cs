using System.Security.Claims;
using FireChart.Viewer.Core;
using Flox.Core.Services;
using Flox.Shared.Bootstrap;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace FireChart.Viewer.App;

// The client side of Flox without an application server: no OIDC sign-in, no OData, no settings of a server. The services of the Viewer are its
// [FloxService] types; the application adds the vendor templates and an anonymous user
public sealed class ViewerAppBootstrapper : FloxBootstrapper
{
    public ViewerAppBootstrapper(IServiceCollection services, IConfigurationBuilder configuration, ILoggingBuilder loggingBuilder, string? baseAddress, string? environment)
        : base(services, configuration, loggingBuilder, baseAddress, environment)
    {
        Services.AddSingleton<IFloxRuntime>(sp => new ViewerAppRuntime(sp.GetRequiredService<IWebAssemblyHostEnvironment>()));
        Services.AddScoped<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();
        Services.AddScoped<IAuthenticationService, AnonymousAuthenticationService>();
        Services.AddScoped<ITemplateRenderers, VendorTemplateRenderers>();
        Services.AddScoped<ViewerAppConfigLoader>();
        Services.AddScoped<Sandbox.DemoPresets>();
        AddModule(new FhirViewerCoreModule());
    }

    protected override IReadOnlyList<string> FloxAssemblyNamePrefixes => ["Flox.", "FireChart."];
}

internal sealed class ViewerAppRuntime : FloxRuntime
{
    private readonly IWebAssemblyHostEnvironment _environment;

    public ViewerAppRuntime(IWebAssemblyHostEnvironment environment)
    {
        _environment = environment;
        BaseAddress = environment.BaseAddress;
    }

    public override FloxPlatform Platform => FloxPlatform.Wasm;

    public override string EnvironmentName => _environment.Environment;
}

// Nobody signs in to the application itself; a FHIR server is signed in to by SMART, inside the Viewer
internal sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> s_anonymous = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return s_anonymous;
    }
}

// The widgets of Flox ask for the user of the application; here there is none, and nobody can sign in
internal sealed class AnonymousAuthenticationService : IAuthenticationService
{
    private static readonly ClaimsPrincipal s_anonymous = new(new ClaimsIdentity());

#pragma warning disable CA1003, CS0067
    public event AuthenticationStateChangedHandler? AuthenticationStateChanged;
#pragma warning restore CA1003, CS0067

    public Task<string?> GetAccessTokenAsync()
    {
        return Task.FromResult<string?>(null);
    }

    public Task<ClaimsPrincipal> GetCurrentUserAsync()
    {
        return Task.FromResult(s_anonymous);
    }

    public Task GoToProfileAsync()
    {
        return Task.CompletedTask;
    }

    public Task LoginAsync(string? returnUrl = null)
    {
        return Task.CompletedTask;
    }

    public Task LogoutAsync()
    {
        return Task.CompletedTask;
    }

    public void SetCurrentUser(ClaimsPrincipal? value)
    {
    }
}
