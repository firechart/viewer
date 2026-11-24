using BlazorBlueprint.Components;
using Flox.Services;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flox.Shared.Components;

/// <summary>
/// Registers the services of the components of Flox.
/// </summary>
public static class FloxComponentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Blazor Blueprint (its portals, overlays, toasts), the dialogs of Flox (<see cref="FloxDialogService"/>) and its snack messages (<see cref="FloxSnackbar"/>);
    /// in the browser puts <see cref="ImportRetryingJSRuntime"/> over the runtime of Blazor WebAssembly, so the modules of Blueprint are imported again
    /// when the interop answers null.
    /// </summary>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddFloxComponents(this IServiceCollection services)
    {
        services.AddBlazorBlueprintComponents();
        if (OperatingSystem.IsBrowser()
            && services.LastOrDefault(d => d.ServiceType == typeof(IJSRuntime)) is { ImplementationInstance: IJSInProcessRuntime runtime and not ImportRetryingJSRuntime })
        {
            services.Replace(ServiceDescriptor.Singleton<IJSRuntime>(new ImportRetryingJSRuntime(runtime)));
        }

        services.TryAddScoped<FloxDialogService>();
        services.TryAddScoped<FloxSnackbar>();
        return services;
    }
}
