using System.Reflection;

namespace Flox.Shared.Layout;

/// <summary>
/// The root component of a Flox application: the router over the assemblies of the shell.
/// </summary>
public partial class FloxApplication
{
    [Inject]
    private IFloxShell FloxHost { get; set; } = default!;

    private IEnumerable<Assembly> AdditionalAssemblies
    {
        get
        {
            return FloxHost.BlazorAssemblies;
        }
    }
}
