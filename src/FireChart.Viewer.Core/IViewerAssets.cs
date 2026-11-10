namespace FireChart.Viewer.Core;

/// <summary>
/// Reads a static file of the host: a file of the <c>wwwroot</c> of the application or of a library it references
/// (<c>_content/{library}/...</c>), by its path relative to the base of the application. The Viewer reads the vendor search forms this way, one
/// form when it is first needed, instead of carrying them all in an assembly.
/// </summary>
/// <remarks>
/// In the browser the implementation is a GET relative to the base address of the page (<c>&lt;base href&gt;</c>), so the files are cached by the
/// browser like the other static files; a path may carry a query (<c>?v=</c>, the version of the library) that makes a new release a new address.
/// Blazor Hybrid (MAUI) has no implementation yet: a .NET <see cref="System.Net.Http.HttpClient"/> does not reach the files the web view serves,
/// they are read from the package of the app there (<c>FileSystem.OpenAppPackageFileAsync("wwwroot/_content/...")</c>).
/// </remarks>
/// <example>
/// <code>
/// var text = await assets.ReadTextAsync("_content/FireChart.Viewer.Library/search-forms/r4/Patient.json?v=1.2.0", cancellationToken);
/// if (text is not null)
/// {
///     var form = ViewerJson.Read&lt;SearchFormDefinition&gt;(text);
/// }
/// </code>
/// </example>
public interface IViewerAssets
{
    /// <summary>
    /// Reads a static file as text.
    /// </summary>
    /// <param name="path">The path relative to the base of the application, without a leading slash, optionally with a query.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text of the file, or <see langword="null"/> when there is no such file.</returns>
    /// <exception cref="System.Net.Http.HttpRequestException">The file could not be read for another reason (the network, the server).</exception>
    Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken);
}
