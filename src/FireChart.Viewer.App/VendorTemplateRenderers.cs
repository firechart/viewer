using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.App;

// The vendor library, its templates read from the static files when they are needed; the overrides of a customer (advanced.templates) come later
internal sealed class VendorTemplateRenderers(IViewerAssets assets, ILogger<VendorTemplateRenderers> logger) : ITemplateRenderers
{
    private string? _style;
    private FhirRenderer? _renderer;

    public Task<FhirRenderer> GetAsync(CancellationToken cancellationToken = default)
    {
        _renderer ??= new FhirRenderer([new WebVendorTemplates(assets)], problem => logger.LogWarning("Template: {Problem}", problem));
        return Task.FromResult(_renderer);
    }

    public async Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
    {
        return _style ??= (await FxStyle.LoadAsync(assets, cancellationToken)).Compose(null);
    }
}
