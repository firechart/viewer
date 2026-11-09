using System.Reflection;
using Microsoft.AspNetCore.Components.Authorization;

namespace Flox.Shared.Layout;

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

    protected override async Task OnInitializedAsync()
    {
        var state = await Asp.GetAuthenticationStateAsync();
        var user = state.User;
    }
}
