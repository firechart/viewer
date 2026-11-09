using System.Reflection;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Settings;

// The settings classes ([FloxSettings]) of every Flox assembly the application has loaded
public interface ISettingsCatalog
{
    IReadOnlyList<SettingsSectionDescriptor> Sections { get; }
}

[FloxService(typeof(ISettingsCatalog), ServiceLifetime.Singleton)]
public sealed class SettingsCatalog
(
    IBootstrapper bootstrapper
) : ISettingsCatalog
{
    private readonly Lazy<IReadOnlyList<SettingsSectionDescriptor>> _sections = new Lazy<IReadOnlyList<SettingsSectionDescriptor>>(() => Scan(bootstrapper.FloxAssemblies));

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
