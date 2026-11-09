using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace FireChart.Viewer.Routing;

/// <summary>
/// Where the Viewer keeps its address. The routing of the Viewer is experimental.
/// </summary>
/// <example>
/// <code>
/// using IViewerLocationStore store = new UrlLocationStore(navigation, "server/42/");
/// store.LocationChanged += (_, _) =&gt; Console.WriteLine(store.Location);
/// store.NavigateTo("resources/Patient");
/// </code>
/// </example>
public interface IViewerLocationStore : IDisposable
{
    /// <summary>
    /// The address of the Viewer relative to its prefix, with the query (<c>resources/Patient?family=Smith</c>); <see langword="null"/> when the address of
    /// the application is not below the prefix.
    /// </summary>
    string? Location { get; }

    /// <summary>
    /// Raised when <see cref="Location"/> has changed (a link, the Back button, <see cref="NavigateTo"/>).
    /// </summary>
    event EventHandler? LocationChanged;

    /// <summary>
    /// Goes to an address of the Viewer.
    /// </summary>
    /// <param name="location">The address relative to the prefix.</param>
    /// <param name="replace">Replace the entry of the history instead of adding one.</param>
    /// <exception cref="ArgumentException">The address could leave the prefix (<see cref="ViewerRoutes.IsSafe"/>).</exception>
    void NavigateTo(string location, bool replace = false);

    /// <summary>
    /// The address as an <c>href</c> of the document: relative to its base, so a link works with the router of the application.
    /// </summary>
    /// <param name="location">The address relative to the prefix.</param>
    /// <returns>The <c>href</c> (<c>server/42/resources/Patient/1</c>).</returns>
    /// <exception cref="ArgumentException">The address could leave the prefix.</exception>
    string HrefOf(string location);
}

/// <summary>
/// The address of the Viewer in the URL of the application, below a prefix, through the <see cref="NavigationManager"/>: the Back button, links and the
/// address bar work as usual. The routing of the Viewer is experimental.
/// </summary>
/// <example>
/// <code>
/// using var store = new UrlLocationStore(navigation, $"server/{id}/");
/// var route = ViewerRoutes.Parse(store.Location);
/// </code>
/// </example>
public sealed class UrlLocationStore : IViewerLocationStore
{
    private readonly NavigationManager _navigation;

    /// <summary>
    /// Creates the store and follows the changes of the address.
    /// </summary>
    /// <param name="navigation">The navigation of the application.</param>
    /// <param name="prefix">The path below the base of the application the Viewer owns (<c>server/42/</c>); empty for the whole application. A leading
    /// slash does not matter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="navigation"/> is <see langword="null"/>.</exception>
    public UrlLocationStore(NavigationManager navigation, string? prefix)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        _navigation = navigation;
        Prefix = Normalize(prefix);
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// The prefix, without a leading slash and with a trailing one (empty for none).
    /// </summary>
    public string Prefix { get; }

    /// <inheritdoc/>
    public event EventHandler? LocationChanged;

    /// <inheritdoc/>
    public string? Location
    {
        get
        {
            var relative = _navigation.ToBaseRelativePath(_navigation.Uri);
            var hash = relative.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                relative = relative[..hash];
            }

            if (Prefix.Length == 0)
            {
                return relative;
            }

            var bare = Prefix.TrimEnd('/');
            if (relative.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return relative[Prefix.Length..];
            }

            return relative.Equals(bare, StringComparison.OrdinalIgnoreCase) || relative.StartsWith(bare + "?", StringComparison.OrdinalIgnoreCase)
                ? relative[bare.Length..].TrimStart('?') is { Length: > 0 } query ? "?" + query : string.Empty
                : null;
        }
    }

    /// <inheritdoc/>
    public void NavigateTo(string location, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!ViewerRoutes.IsSafe(location))
        {
            throw new ArgumentException("Not an address of the viewer.", nameof(location));
        }

        _navigation.NavigateTo(Prefix + location, new NavigationOptions { ReplaceHistoryEntry = replace });
    }

    /// <inheritdoc/>
    public string HrefOf(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!ViewerRoutes.IsSafe(location))
        {
            throw new ArgumentException("Not an address of the viewer.", nameof(location));
        }

        return Prefix + location;
    }

    /// <summary>
    /// Stops following the changes of the address.
    /// </summary>
    public void Dispose()
    {
        _navigation.LocationChanged -= OnLocationChanged;
    }

    private static string Normalize(string? prefix)
    {
        var trimmed = (prefix ?? string.Empty).Trim().TrimStart('/');
        return trimmed.Length == 0 || trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        LocationChanged?.Invoke(this, EventArgs.Empty);
    }
}
