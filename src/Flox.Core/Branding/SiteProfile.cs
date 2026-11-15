namespace Flox.Core.Branding;

/// <summary>
/// Where the logo of the organization stands in the header.
/// </summary>
public enum OrganizationLogoPlacement
{
    /// <summary>
    /// At the right of the header; the application logo stays at the left.
    /// </summary>
    Right,

    /// <summary>
    /// In front of the application logo.
    /// </summary>
    BeforeApplication
}

/// <summary>
/// Named text values: an absent name is an empty string, so a template can test it.
/// </summary>
/// <param name="values">The values by name.</param>
public sealed class ProfileValues(IReadOnlyDictionary<string, string> values)
{
    /// <summary>
    /// The value of a name, or an empty string when there is none.
    /// </summary>
    public string this[string name]
    {
        get
        {
            return values.GetValueOrDefault(name) ?? string.Empty;
        }
    }

    /// <summary>
    /// Tells whether the name has a non-empty value.
    /// </summary>
    public bool Has(string name)
    {
        return this[name].Length > 0;
    }
}

/// <summary>
/// What the public pages and the texts of a site know about whose application it is.
/// </summary>
public sealed class SiteProfile
{
    /// <summary>
    /// The application (name, tagline, description, author, company, copyright); only its vendor changes it.
    /// </summary>
    public required ProfileValues App { get; init; }
    /// <summary>
    /// The organization the application runs for (a clinic, a hospital); empty unless there is one.
    /// </summary>
    public required ProfileValues Org { get; init; }
    /// <summary>
    /// Flox itself, fixed in code.
    /// </summary>
    public required ProfileValues Platform { get; init; }
    /// <summary>
    /// Address of the logo of the organization.
    /// </summary>
    public string? OrganizationLogoUrl { get; init; }

    /// <summary>
    /// The installation's own stylesheet (site.css of its custom files), loaded after the one of the site.
    /// </summary>
    public string? CustomStylesheetUrl { get; init; }
    /// <summary>
    /// Where the logo of the organization stands.
    /// </summary>
    public OrganizationLogoPlacement OrganizationLogoPlacement { get; init; }

    /// <summary>
    /// Tells whether the site has an organization.
    /// </summary>
    public bool HasOrganization
    {
        get
        {
            return Org.Has("Name");
        }
    }
}

/// <summary>
/// Provides the profile of the site.
/// </summary>
public interface ISiteProfileProvider
{
    /// <summary>
    /// Loads the profile of the site.
    /// </summary>
    Task<SiteProfile> GetAsync();
}
