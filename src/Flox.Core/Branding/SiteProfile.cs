namespace Flox.Core.Branding;

public enum OrganizationLogoPlacement
{
    // At the right of the header, the application logo stays at the left
    Right,

    // In front of the application logo
    BeforeApplication
}

// Named text values: an absent name is an empty string, so a template can test it
public sealed class ProfileValues(IReadOnlyDictionary<string, string> values)
{
    public string this[string name]
    {
        get
        {
            return values.GetValueOrDefault(name) ?? string.Empty;
        }
    }

    public bool Has(string name)
    {
        return this[name].Length > 0;
    }
}

// What the public pages and the texts of a site know about whose application it is.
// App: the application (name, tagline, description, author, company, copyright...); only its vendor changes it.
// Org: the organization the application runs for (a clinic, a hospital), empty unless there is one.
// Platform: Flox itself, fixed in code
public sealed class SiteProfile
{
    public required ProfileValues App { get; init; }
    public required ProfileValues Org { get; init; }
    public required ProfileValues Platform { get; init; }
    public string? OrganizationLogoUrl { get; init; }

    // The installation's own stylesheet (site.css of its custom files), loaded after the one of the site
    public string? CustomStylesheetUrl { get; init; }
    public OrganizationLogoPlacement OrganizationLogoPlacement { get; init; }

    public bool HasOrganization
    {
        get
        {
            return Org.Has("Name");
        }
    }
}

public interface ISiteProfileProvider
{
    Task<SiteProfile> GetAsync();
}
