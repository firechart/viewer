using FireChart.Viewer.Core;
using FireChart.Viewer.Library;
using Flox.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FireChart.Viewer.Appearance;

// The appearance of a component of the Viewer (FhirViewer, FhirBundleVisualizer): the stylesheet composed once per appearance (with the theme of the
// library it names) and the theme set on the document only when the appearance asks for one (in a Flox application the host owns it)
internal sealed class AppearanceApplier(IViewerAssets assets, IJSRuntime js, ILogger logger) : IAsyncDisposable
{
    private ViewerAppearance? _composedFor;
    private ViewerTheme? _appliedTheme;
    private IJSObjectReference? _themeModule;

    public AppearanceStylesheet Stylesheet { get; private set; } = new(string.Empty, []);

    // The errors of a new appearance, none when it is the one composed already
    public async Task<IReadOnlyList<ViewerConfigError>> ComposeAsync(ViewerAppearance? appearance)
    {
        if (ReferenceEquals(_composedFor, appearance))
        {
            return [];
        }

        _composedFor = appearance;
        Stylesheet = AppearanceCss.Compose(appearance, await LoadThemeAsync(appearance));
        foreach (var error in Stylesheet.Errors)
        {
            logger.LogWarning("Appearance: {Param}: {Message}", error.Param, error.Message);
        }

        return Stylesheet.Errors;
    }

    public async Task ApplyThemeAsync(ViewerAppearance? appearance)
    {
        var theme = appearance?.Theme ?? ViewerTheme.Inherit;
        if (theme == ViewerTheme.Inherit || theme == _appliedTheme)
        {
            return;
        }

        _appliedTheme = theme;
        _themeModule ??= await js.ImportModuleAsync("./_content/FireChart.Viewer/js/viewer-theme.js");
        await _themeModule.InvokeVoidAsync("setTheme", theme.ToString().ToLowerInvariant());
    }

    // The base of a theme and its file, when the appearance makes the Viewer its own; none on an error (the vendor look stays)
    private async Task<FxThemeSheets?> LoadThemeAsync(ViewerAppearance? appearance)
    {
        if (!AppearanceCss.IsThemed(appearance))
        {
            return null;
        }

        try
        {
            return await FxStyle.LoadThemeAsync(assets, appearance!.Style);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "The theme of the Viewer could not be read");
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_themeModule is not null)
        {
            try
            {
                await _themeModule.InvokeVoidAsync("stop");
                await _themeModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the page is gone
            }
        }
    }
}
