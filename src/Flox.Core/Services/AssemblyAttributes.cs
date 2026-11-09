using System.Reflection;

namespace Flox.Core.Services;

public static class AssemblyAttributes
{
    private static readonly Assembly? s_assembly = Assembly.GetCallingAssembly();

    private static readonly Lazy<string?> s_authors = new Lazy<string?>(() => s_assembly?.GetCustomAttributes<AssemblyMetadataAttribute>()
                                                                                         .FirstOrDefault(a => a.Key == "Authors")
                                                                                        ?.Value ?? s_assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()
                                                                                                             ?.Company);

    private static readonly Lazy<string?> s_assemblyName = new Lazy<string?>(() => s_assembly?.GetName()
                                                                                              .Name);

    private static readonly Lazy<string?> s_company = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()
                                                                                        ?.Company);

    private static readonly Lazy<string?> s_configuration = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyConfigurationAttribute>()
                                                                                              ?.Configuration);

    private static readonly Lazy<string?> s_copyright = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyCopyrightAttribute>()
                                                                                          ?.Copyright);

    private static readonly Lazy<string?> s_culture = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyCultureAttribute>()
                                                                                        ?.Culture);

    private static readonly Lazy<string?> s_description = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyDescriptionAttribute>()
                                                                                            ?.Description);

    private static readonly Lazy<string?> s_fileVersion = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()
                                                                                            ?.Version);

    private static readonly Lazy<string?> s_informationalVersion = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                                                                     ?.InformationalVersion);

    private static readonly Lazy<string?> s_product = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyProductAttribute>()
                                                                                        ?.Product);

    private static readonly Lazy<string?> s_title = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyTitleAttribute>()
                                                                                      ?.Title);

    private static readonly Lazy<string?> s_trademark = new Lazy<string?>(() => s_assembly?.GetCustomAttribute<AssemblyTrademarkAttribute>()
                                                                                          ?.Trademark);

    private static readonly Lazy<string?> s_version = new Lazy<string?>(() => s_assembly?.GetName()
                                                                                         .Version
                                                                                        ?.ToString());

    public static string? AssemblyName
    {
        get
        {
            return s_assemblyName.Value;
        }
    }

    public static string? Authors
    {
        get
        {
            return s_authors.Value;
        }
    }

    public static string? Company
    {
        get
        {
            return s_company.Value;
        }
    }

    public static string? Configuration
    {
        get
        {
            return s_configuration.Value;
        }
    }

    public static string? Copyright
    {
        get
        {
            return s_copyright.Value;
        }
    }

    public static string? Culture
    {
        get
        {
            return s_culture.Value;
        }
    }

    public static string? Description
    {
        get
        {
            return s_description.Value;
        }
    }

    public static string? FileVersion
    {
        get
        {
            return s_fileVersion.Value;
        }
    }

    public static string? InformationalVersion
    {
        get
        {
            return s_informationalVersion.Value;
        }
    }

    public static string? Product
    {
        get
        {
            return s_product.Value;
        }
    }

    public static string? Title
    {
        get
        {
            return s_title.Value;
        }
    }

    public static string? Trademark
    {
        get
        {
            return s_trademark.Value;
        }
    }

    public static string? Version
    {
        get
        {
            return s_version.Value;
        }
    }
}
