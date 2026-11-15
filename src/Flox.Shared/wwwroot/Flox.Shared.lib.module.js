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

// A content blocker may refuse a script (net::ERR_BLOCKED_BY_CLIENT, cookie banner lists): the app starts without it
async function loadOptionalScript(src) {
    try {
        await loadScript(src);
    } catch {
        console.warn(`Script not loaded (blocked?): ${src}`);
    }
}

async function loadPostStartScripts() {
    await loadOptionalScript('_content/Flox.Shared/blazor-error-ui.js');

    // A service worker is the host page's own (Pro: ServerApp.razor; Lite: when it has one): a shared library cannot know whether the
    // application has the file, and the page of an application without it answered service-worker.js with HTML
}

// Standalone WASM / Blazor Server / MAUI Hybrid
export async function beforeStart(_options, _extensions) {
}

export async function afterStarted(_blazor) {
    await loadPostStartScripts();
}

// WASM
export async function beforeWebAssemblyStart(_options, _extensions) {
}

export async function afterWebAssemblyStarted(_blazor) {
    await loadPostStartScripts();
}
