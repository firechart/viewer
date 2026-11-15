using System.Reflection;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Settings;

/// <summary>The settings classes (<c>[FloxSettings]</c>) of every Flox assembly the application has loaded.</summary>
public interface ISettingsCatalog
{
    /// <summary>The sections in display order.</summary>
    IReadOnlyList<SettingsSectionDescriptor> Sections { get; }
}

/// <summary>The default <see cref="ISettingsCatalog"/>: finds the settings classes in the Flox assemblies of the host on first use.</summary>
/// <param name="bootstrapper">The bootstrapper that knows the Flox assemblies.</param>
[FloxService(typeof(ISettingsCatalog), ServiceLifetime.Singleton)]
public sealed class SettingsCatalog
(
    IBootstrapper bootstrapper
) : ISettingsCatalog
{
    private readonly Lazy<IReadOnlyList<SettingsSectionDescriptor>> _sections = new Lazy<IReadOnlyList<SettingsSectionDescriptor>>(() => Scan(bootstrapper.FloxAssemblies));

    /// <inheritdoc />
    public IReadOnlyList<SettingsSectionDescriptor> Sections
    {
        get
        {
            return _sections.Value;
        }
    }

    private static IReadOnlyList<SettingsSectionDescriptor> Scan(IEnumerable<Assembly> assemblies)
    {
        return assemblies.SelectMany(assembly => assembly.GetExportedTypes())
                         .Where(type => type is { IsClass: true, IsAbstract: false })
                         .Select(SettingsSectionDescriptor.Create)
                         .OfType<SettingsSectionDescriptor>()
                         .OrderBy(section => section.Order)
                         .ThenBy(section => section.Title)
                         .ToList();
    }
}
