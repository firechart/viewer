using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.App;

// The vendor library the package carries; the overrides of a customer (advanced.templates) come later
internal sealed class VendorTemplateRenderers(ILogger<VendorTemplateRenderers> logger) : ITemplateRenderers
{
    private static readonly string s_style = FxStyle.Compose(null);
    private FhirRenderer? _renderer;

    public Task<FhirRenderer> GetAsync(CancellationToken cancellationToken = default)
    {
        _renderer ??= new FhirRenderer([new VendorTemplates()], problem => logger.LogWarning("Template: {Problem}", problem));
        return Task.FromResult(_renderer);
    }

    public Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(s_style);
    }
}
