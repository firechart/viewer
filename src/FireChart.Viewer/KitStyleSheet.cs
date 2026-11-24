using Flox.Services;
using Microsoft.JSInterop;

namespace FireChart.Viewer;

// The CSS of the kit (IViewerStyle) as one sheet in the head of the document, put there before the first rows and kept (viewer-style.js). The pages
// of the Viewer (the types, a list, a record) render no <style> of their own: each one came and went with its page, in the same render as the rows,
// and WebKit registers the font faces again whenever a sheet comes or goes. On iOS (Lite installed, 2026-11-23) the first page of a list then showed
// empty boxes instead of the icons of the types (the zero-size fallback of the icon font) and titles cut short, until a scroll or a second visit
internal static class KitStyleSheet
{
    private const string Module = "./_content/FireChart.Viewer/js/viewer-style.js";

    public static async Task AdoptAsync(IJSRuntime js, string css)
    {
        if (css.Length == 0)
        {
            return;
        }

        var module = await js.ImportModuleAsync(Module);
        try
        {
            await module.InvokeVoidAsync("adopt", css);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }
}
