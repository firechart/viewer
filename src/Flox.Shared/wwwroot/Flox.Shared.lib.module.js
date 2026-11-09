// Blazor JS Initializer for Flox.Shared RCL (.NET 10).
// Detected and imported automatically by Blazor — no <script> tags needed in index.html.
// Docs: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/startup?view=aspnetcore-10.0
function loadScript(src) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[src="${src}"]`)) {
            resolve();
            return;
        }
        const script = document.createElement('script');
        script.src = src;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

async function loadCoreScripts() {
    await loadScript('_content/Flox.Shared/lib/cookieconsent/dist/cookieconsent.umd.min.js');
}

async function loadPostStartScripts() {
    await loadScript('_content/Flox.Shared/blazor-error-ui.js');
    await loadScript('_content/Flox.Shared/cookie-gdpr.js');

    // Register PWA service worker in production only. Excludes MAUI's BlazorWebView: it's not a
    // browser (no install prompt, no offline-caching model a SW would help with), it has no
    // service-worker.js of its own, and its virtual host ("0.0.0.1", per BlazorWebView's default
    // AppHostAddress - see the error this guarded against) would otherwise slip past the
    // localhost/127.0.0.1 check below and register against a nonexistent script. Detected via the
    // blazor.webview.js script tag, which only ever appears on the MAUI host page, rather than
    // hardcoding the "0.0.0.1" hostname (which is an implementation detail, not a contract).
    const isBlazorWebView = !!document.querySelector('script[src*="blazor.webview.js"]');
    if (!isBlazorWebView &&
        'serviceWorker' in navigator &&
        window.location.hostname !== 'localhost' &&
        window.location.hostname !== '127.0.0.1') {
        // Registration failure (stale cache, network hiccup, ...) shouldn't be treated as an
        // app-fatal error - it's degraded offline capability, not a broken app.
        navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
            .catch((err) => console.warn('Service worker registration failed:', err));
    }
}

// Standalone WASM / Blazor Server / MAUI Hybrid
export async function beforeStart(_options, _extensions) {
    await loadCoreScripts();
}

export async function afterStarted(_blazor) {
    await loadPostStartScripts();
}

// WASM
export async function beforeWebAssemblyStart(_options, _extensions) {
    await loadCoreScripts();
}

export async function afterWebAssemblyStarted(_blazor) {
    await loadPostStartScripts();
}
