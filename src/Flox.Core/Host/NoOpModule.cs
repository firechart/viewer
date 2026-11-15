namespace Flox.Core.Host;

/// <summary>A module that does nothing; replaces, through <see cref="Bootstrapper.ReplaceModule(System.Type, BootstrapModule)"/>, a module that a shared base class always adds and that does not apply to a platform.</summary>
public sealed class NoOpModule : BootstrapModule
{
    /// <summary>Does nothing.</summary>
    public override void ConfigureServices()
    {
    }
}
