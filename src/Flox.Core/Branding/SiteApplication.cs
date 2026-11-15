namespace Flox.Core.Branding;

/// <summary>
/// An application the home page of an installation offers (the launcher). Register one per application with services.AddSingleton(new SiteApplication(...)).
/// </summary>
/// <param name="Title">Name of the application.</param>
/// <param name="Url">Address on the site, for example "~/app/".</param>
/// <param name="Description">Short description.</param>
/// <param name="Icon">Path of a static file, for example "_content/MyApp.Shared/icon.svg".</param>
/// <param name="Order">Position in the launcher; lower comes first.</param>
public sealed record SiteApplication(string Title, string Url, string? Description = null, string? Icon = null, int Order = 0);
