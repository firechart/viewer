using System.Reflection;
using Microsoft.AspNetCore.Components.Authorization;

namespace Flox.Shared.Layout;

/// <summary>
/// The root component of a Flox application: the router over the assemblies of the shell.
/// </summary>
public partial class FloxApplication
{
    [Inject]
    private IFloxShell FloxHost { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Asp { get; set; } = default!;

    private IEnumerable<Assembly> AdditionalAssemblies
    {
        get
        {
            return FloxHost.BlazorAssemblies;
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var state = await Asp.GetAuthenticationStateAsync();
        var user = state.User;
    }
}
