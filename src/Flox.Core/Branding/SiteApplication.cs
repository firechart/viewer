namespace Flox.Core.Branding;

// An application the home page of an installation offers (the launcher): register one per application with services.AddSingleton(new SiteApplication(...)).
// Url is an address of the site ("~/app/"), Icon a path of a static file ("_content/MyApp.Shared/icon.svg")
public sealed record SiteApplication(string Title, string Url, string? Description = null, string? Icon = null, int Order = 0);
