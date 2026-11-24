namespace FireChart.Viewer.Core;

/// <summary>
/// The CSS of the kit <c>fx-*</c> of the library (the vendor's, with a customer's layer over it): the pages of the Viewer put it in one
/// <c>&lt;style&gt;</c> in the head of the document, once, so the blocks of the components have their look.
/// </summary>
/// <remarks>The text is read once per scope and kept; it does not change while the application runs.</remarks>
/// <example>
/// <code>
/// var css = await style.GetStyleAsync();
/// </code>
/// </example>
public interface IViewerStyle
{
    /// <summary>
    /// The CSS of the kit.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading of the files of the library.</param>
    /// <returns>The CSS text; empty when the library has none.</returns>
    /// <example>
    /// <code>
    /// &lt;style&gt;@await Style.GetStyleAsync()&lt;/style&gt;
    /// </code>
    /// </example>
    Task<string> GetStyleAsync(CancellationToken cancellationToken = default);
}
