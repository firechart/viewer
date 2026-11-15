using System.Reflection;

namespace Flox.Core.Services;

/// <summary>The assembly metadata (name, version, company, ...) of the assembly that first used this class.</summary>
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

    /// <summary>The simple name of the assembly.</summary>
    public static string? AssemblyName
    {
        get
        {
            return s_assemblyName.Value;
        }
    }

    /// <summary>The authors; falls back to the company.</summary>
    public static string? Authors
    {
        get
        {
            return s_authors.Value;
        }
    }

    /// <summary>The company.</summary>
    public static string? Company
    {
        get
        {
            return s_company.Value;
        }
    }

    /// <summary>The build configuration, such as Debug or Release.</summary>
    public static string? Configuration
    {
        get
        {
            return s_configuration.Value;
        }
    }

    /// <summary>The copyright notice.</summary>
    public static string? Copyright
    {
        get
        {
            return s_copyright.Value;
        }
    }

    /// <summary>The neutral culture of the assembly.</summary>
    public static string? Culture
    {
        get
        {
            return s_culture.Value;
        }
    }

    /// <summary>The description.</summary>
    public static string? Description
    {
        get
        {
            return s_description.Value;
        }
    }

    /// <summary>The file version.</summary>
    public static string? FileVersion
    {
        get
        {
            return s_fileVersion.Value;
        }
    }

    /// <summary>The informational version, which usually holds the product version and the commit.</summary>
    public static string? InformationalVersion
    {
        get
        {
            return s_informationalVersion.Value;
        }
    }

    /// <summary>The product name.</summary>
    public static string? Product
    {
        get
        {
            return s_product.Value;
        }
    }

    /// <summary>The title.</summary>
    public static string? Title
    {
        get
        {
            return s_title.Value;
        }
    }

    /// <summary>The trademark.</summary>
    public static string? Trademark
    {
        get
        {
            return s_trademark.Value;
        }
    }

    /// <summary>The assembly version.</summary>
    public static string? Version
    {
        get
        {
            return s_version.Value;
        }
    }
}
