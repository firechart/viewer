// Blazor JS Initializer for Flox.Shared RCL (.NET 10).
// Detected and imported automatically by Blazor — no <script> tags needed in index.html.
// Docs: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/startup?view=aspnetcore-10.0
import { installEnterKey } from './flox-enter-key.js';
import { installIosRepaint } from './flox-ios-repaint.js';
import { installHistory } from './flox-history.js';

// A keydown without a key (the browser filling a field from its autofill) is stopped before the handlers of the page: the shortcuts of Blazor
// Blueprint read event.key.toLowerCase() on the document and failed on it, which the error UI showed as fatal. Window, capture: before them all
let keylessGuarded = false;

function installKeylessKeydownGuard() {
    if (keylessGuarded) {
        return;
    }
    keylessGuarded = true;
    window.addEventListener('keydown', event => {
        if (typeof event.key !== 'string') {
            event.stopImmediatePropagation();
        }
    }, true);
}

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

// Standalone WASM / Blazor Server
export async function beforeStart(_options, _extensions) {
    installEnterKey();
    installKeylessKeydownGuard();
    installHistory();
}

export async function afterStarted(_blazor) {
    installIosRepaint();
    await loadPostStartScripts();
}

// WASM
export async function beforeWebAssemblyStart(_options, _extensions) {
    installEnterKey();
    installKeylessKeydownGuard();
    installHistory();
}

export async function afterWebAssemblyStarted(_blazor) {
    installIosRepaint();
    await loadPostStartScripts();
}
