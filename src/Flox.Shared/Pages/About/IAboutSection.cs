namespace Flox.Shared.Pages.About;

/// <summary>
/// A section a module adds to About: a component without parameters, shown under the license of the application and above its components.
/// </summary>
/// <remarks>
/// Register it with <c>[FloxService(typeof(IAboutSection), ServiceLifetime.Singleton, Multiple = true)]</c>.
/// </remarks>
public interface IAboutSection
{
    /// <summary>The type of the component that renders the section.</summary>
    Type Component { get; }
}
