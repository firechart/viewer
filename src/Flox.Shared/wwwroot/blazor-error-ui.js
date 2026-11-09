// Fatal error UI - built entirely at runtime, deliberately outside the Blazor
// component tree. This is the layer below AppErrorBoundary/ErrorBoundary: it
// exists precisely for the cases where Blazor itself never got far enough to
// render anything (WASM DI container failure, a circuit that never came up),
// so it cannot be a Razor component and must not depend on Blazor having
// started.
//
// id="blazor-error-ui" and classes "reload"/"dismiss" are a hardcoded contract
// of Blazor's own framework scripts (blazor.web.js / blazor.webassembly.js):
// they call document.querySelector("#blazor-error-ui") directly on certain
// internal failures (WASM callEntryPoint throwing, a circuit "JS.Error") and
// set its style.display = "block" themselves, plus auto-wire .reload/.dismiss
// click handlers. None of that is under our control, so this file must keep
// that exact id, and the display toggle is done via a CSS class marked
// !important (below) rather than by trying to out-write Blazor's own inline
// style.
//
// Markup AND styles are both built here, injected as a <style> tag - nothing
// else on any host page is required beyond a single <script src="..."> tag.
(function () {
    // Every error of the page in the order it came: console errors (not fatal on their own, see below, but the cause of a fatal
    // one is usually among them) and the fatal ones. The first ones are always kept: after the runtime dies every call into it
    // fails with ".NET runtime already exited", and a list of the last ones would show only that
    const KEEP_FIRST = 10;
    const KEEP_LAST = 20;
    const timeline = [];
    // A dismissed dialog comes back for a new error, not for another failure of the dead runtime
    let dismissed = false;
    const AFTER_EXIT = /\.NET runtime already exited/;

    // Colors/fonts prefer Radzen's own theme variables (--rz-*) when the Radzen
    // theme has actually loaded, falling
    // back to the --blazor-error-* set below when it hasn't (which is exactly
    // the case this whole UI exists for).
    const STYLES = `
        :root {
            --blazor-error-font: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji";
            --blazor-error-font-mono: ui-monospace, "SF Mono", SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
            --blazor-error-bg-overlay: rgba(17, 17, 20, 0.55);
            --blazor-error-bg-card: #FFFFFF;
            --blazor-error-bg-pre: #F2F2F3;
            --blazor-error-border: #E3E3E6;
            --blazor-error-text-title: #1A1A1E;
            --blazor-error-text-body: #55565C;
            --blazor-error-text-pre: #55565C;
            --blazor-error-accent: #D33A3A;
            --blazor-error-btn-reload-bg: #2E5AAC;
            --blazor-error-btn-reload-text: #FFFFFF;
            --blazor-error-btn-details-text: #55565C;
            --blazor-error-shadow: 0 12px 40px rgba(0, 0, 0, 0.18), 0 2px 8px rgba(0, 0, 0, 0.08);
        }

        @media (prefers-color-scheme: dark) {
            :root {
                --blazor-error-bg-overlay: rgba(0, 0, 0, 0.65);
                --blazor-error-bg-card: #1C1C1F;
                --blazor-error-bg-pre: #26262A;
                --blazor-error-border: #34343A;
                --blazor-error-text-title: #F2F2F3;
                --blazor-error-text-body: #B9BABF;
                --blazor-error-text-pre: #B9BABF;
                --blazor-error-accent: #E36767;
                --blazor-error-btn-reload-bg: #4B76C4;
                --blazor-error-btn-reload-text: #FFFFFF;
                --blazor-error-btn-details-text: #B9BABF;
                --blazor-error-shadow: 0 12px 40px rgba(0, 0, 0, 0.5), 0 2px 8px rgba(0, 0, 0, 0.3);
            }
        }

        /* The classic bottom-banner template convention styles #blazor-error-ui
           too (background-color, color, padding, box-shadow), and a stylesheet
           loaded after this <style> tag wins on equal specificity - hence
           !important on every property it may also set, to keep this dialog
           looking the way we designed it regardless of load order. */
        #blazor-error-ui {
            display: none;
            position: fixed;
            inset: 0;
            background: var(--blazor-error-bg-overlay) !important;
            color: inherit !important;
            box-shadow: none !important;
            z-index: 9999;
            font-family: var(--rz-text-font-family, var(--blazor-error-font));
            padding: 16px !important;
            box-sizing: border-box;
        }

        /* Added below, regardless of who revealed the element (our own error
           listeners, or Blazor's own hardcoded fallback code) - !important so
           it always wins over the inline style.display="block" that Blazor's
           own framework scripts set directly. */
        #blazor-error-ui.blazor-error-ui-active {
            display: grid !important;
            place-items: center;
        }

        #blazor-error-ui-card {
            position: relative;
            background: var(--rz-base-background-color, var(--blazor-error-bg-card));
            border-radius: var(--rz-border-radius, 14px);
            max-width: 380px;
            max-height: 85vh;
            width: 100%;
            box-shadow: var(--blazor-error-shadow);
            border: 1px solid var(--blazor-error-border);
            padding: 28px 24px 20px;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            text-align: center;
            overflow: hidden;
        }

        /* Small screens (phones): a fullscreen sheet instead of a centered card. */
        @media (max-width: 600px) {
            #blazor-error-ui {
                padding: 0;
            }

            #blazor-error-ui-card {
                max-width: none;
                max-height: none;
                width: 100%;
                height: 100%;
                border-radius: 0;
                border: none;
                justify-content: center;
            }
        }

        #blazor-error-ui-header {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        #blazor-error-ui-icon {
            width: 28px;
            height: 28px;
            fill: var(--rz-danger, var(--blazor-error-accent));
            flex-shrink: 0;
        }

        #blazor-error-ui-icon rect {
            fill: var(--rz-base-background-color, var(--blazor-error-bg-card));
        }

        #blazor-error-ui-title {
            font-size: 16px;
            font-family: inherit;
            font-weight: 600;
            color: var(--rz-text-color, var(--blazor-error-text-title));
            letter-spacing: -0.01em;
        }

        #blazor-error-ui-message {
            margin-top: 6px;
            font-size: 13px;
            font-family: inherit;
            line-height: 1.5;
            color: var(--rz-text-secondary-color, var(--blazor-error-text-body));
        }

        #blazor-error-ui-stack {
            margin-top: 12px;
            width: 100%;
            max-height: 200px;
            overflow: auto;
            text-align: left;
            white-space: pre-wrap;
            word-break: break-word;
            font-family: var(--blazor-error-font-mono);
            font-size: 11px;
            line-height: 1.6;
            color: var(--rz-text-secondary-color, var(--blazor-error-text-pre));
            background: var(--rz-base-200, var(--blazor-error-bg-pre));
            padding: 10px 12px;
            border-radius: 8px;
            border: 1px solid var(--blazor-error-border);
            box-sizing: border-box;
        }

        #blazor-error-ui-actions {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 10px;
            margin-top: 18px;
            width: 100%;
        }

        .blazor-error-reload {
            width: 100%;
            box-sizing: border-box;
            padding: 9px 16px;
            border-radius: var(--rz-border-radius, 8px);
            border: none;
            cursor: pointer;
            font-size: 13px;
            font-family: var(--blazor-error-font);
            font-weight: 600;
            text-decoration: none;
            text-align: center;
            display: inline-block;
            background: var(--rz-primary, var(--blazor-error-btn-reload-bg));
            color: var(--rz-on-primary, var(--blazor-error-btn-reload-text));
        }

        .blazor-error-close {
            position: absolute;
            top: 8px;
            right: 8px;
            width: 32px;
            height: 32px;
            padding: 0;
            border: none;
            border-radius: 8px;
            background: none;
            cursor: pointer;
            font-size: 22px;
            line-height: 32px;
            font-family: var(--blazor-error-font);
            color: var(--rz-text-secondary-color, var(--blazor-error-btn-details-text));
        }

        .blazor-error-close:hover {
            background: var(--rz-base-200, var(--blazor-error-bg-pre));
        }

        .blazor-error-details-toggle {
            padding: 0;
            border: none;
            background: none;
            cursor: pointer;
            font-size: 12px;
            font-family: var(--rz-text-font-family, var(--blazor-error-font));
            color: var(--rz-text-secondary-color, var(--blazor-error-btn-details-text));
            text-decoration: underline;
        }
    `;

    // Idempotent: safe even if this script were ever included twice.
    function ensureStyles() {
        if (document.getElementById('blazor-error-ui-styles')) return;
        const style = document.createElement('style');
        style.id = 'blazor-error-ui-styles';
        style.textContent = STYLES;
        document.head.appendChild(style);
    }

    function buildMarkup() {
        return `
            <div id="blazor-error-ui-card" role="alertdialog" aria-labelledby="blazor-error-ui-title" aria-describedby="blazor-error-ui-message">
                <button type="button" class="dismiss blazor-error-close" aria-label="Close">&times;</button>
                <div id="blazor-error-ui-header">
                    <svg id="blazor-error-ui-icon" viewBox="0 0 24 24" aria-hidden="true">
                        <circle cx="12" cy="12" r="10" />
                        <rect x="11" y="6" width="2" height="8" rx="1" />
                        <rect x="11" y="16" width="2" height="2" rx="1" />
                    </svg>
                    <div id="blazor-error-ui-title">Something went wrong</div>
                </div>
                <div id="blazor-error-ui-message">The application ran into a problem and couldn't continue.</div>
                <pre id="blazor-error-ui-stack" hidden></pre>
                <div id="blazor-error-ui-actions">
                    <button type="button" class="blazor-error-details-toggle">Show details</button>
                    <a href="." class="reload blazor-error-reload">Reload</a>
                </div>
            </div>`;
    }

    // Idempotent: safe even if this script were ever included twice.
    function ensureContainer() {
        let ui = document.getElementById('blazor-error-ui');
        if (ui) return ui;

        ui = document.createElement('div');
        ui.id = 'blazor-error-ui';
        ui.setAttribute('data-nosnippet', '');
        ui.innerHTML = buildMarkup();

        // May run before <body> exists (script sits in <head>); <html> is
        // always available immediately. Positioning is position:fixed, so
        // where in the DOM tree this actually lives doesn't matter visually.
        (document.body || document.documentElement).appendChild(ui);
        return ui;
    }

    ensureStyles();
    const ui = ensureContainer();

    function remember(text, fatal) {
        const kind = AFTER_EXIT.test(text) ? 'exited' : null;
        const last = timeline[timeline.length - 1];
        // The same failure again (the cascade after the runtime died, a render error repeated) is counted, not listed
        if (last && (kind ? last.kind === kind : last.text === text)) {
            last.count++;
            last.fatal = last.fatal || fatal;
            return false;
        }

        timeline.push({ text, kind, fatal, count: 1 });
        if (timeline.length > KEEP_FIRST + KEEP_LAST) {
            timeline.splice(KEEP_FIRST, 1);
        }

        return true;
    }

    function render() {
        const stackEl = document.getElementById('blazor-error-ui-stack');
        if (!stackEl) {
            return;
        }

        const lines = timeline.map((entry, index) => {
            const label = `#${index + 1} ${entry.fatal ? 'fatal' : 'console'}${entry.count > 1 ? `, ${entry.count} times` : ''}`;
            const text = entry.kind === 'exited' ? '.NET runtime already exited: every later call into it fails' : entry.text;
            return `[${label}] ${text}`;
        });
        stackEl.textContent = 'The errors in the order they came; the cause is usually the first fatal one or the console error just before it.\n\n'
            + lines.join('\n\n---\n\n');
    }

    function show() {
        if (ui) {
            ui.style.display = '';
            ui.classList.add('blazor-error-ui-active');
        }
    }

    function hide() {
        dismissed = true;
        ui?.classList.remove('blazor-error-ui-active');
        // Blazor's own scripts reveal the element with an inline style
        if (ui) {
            ui.style.display = 'none';
        }
    }

    function reportFatalError(text) {
        const added = text ? remember(text, true) : false;
        render();
        if (!dismissed || (added && !AFTER_EXIT.test(text))) {
            dismissed = false;
            show();
        }
    }

    function toggleBlazorErrorStack(button) {
        const s = document.getElementById('blazor-error-ui-stack');
        if (!s) return;
        const showing = !s.hidden;
        s.hidden = showing;
        if (button) {
            button.textContent = showing ? 'Show details' : 'Hide details';
        }
    }

    // Whenever ANYONE reveals the element - us, or Blazor's own framework code
    // writing style.display="block" directly on a WASM boot failure / circuit
    // error - make sure it renders through our styling too.
    if (ui && 'MutationObserver' in window) {
        new MutationObserver(() => {
            if (ui.style.display && ui.style.display !== 'none') {
                if (dismissed) {
                    // Blazor reveals the element again on every failure of the dead runtime: a closed dialog stays closed
                    ui.style.display = 'none';
                    return;
                }

                render();
                show();
            }
        }).observe(ui, { attributes: true, attributeFilter: ['style'] });
    }

    // Remembers the last console errors; shows the overlay only for the renderer's fatal log (see below why not for the rest)
    const originalConsoleError = console.error;
    // Blazor's renderer reports an exception that no error boundary can take (a failure while a component is created,
    // for example a missing service) as a fatal log: after it the renderer is dead, so it is shown as fatal
    const FATAL_RENDER_ERROR = /\[ FTL \].*WebAssemblyRenderer.*Unhandled exception rendering component/s;
    console.error = function (...args) {
        const text = args.map((arg) => (arg instanceof Error ? `${arg.message}
${arg.stack ?? ''}` : String(arg))).join(' ');
        remember(text, false);
        originalConsoleError.apply(console, args);
        if (FATAL_RENDER_ERROR.test(text)) {
            reportFatalError(text);
        }
    };

    // Deliberately NOT showing the overlay for every console.error: on a WASM-only host (every app on
    // this platform today - no AddServerSideBlazor/circuit anywhere), the app
    // itself logs plenty of properly-handled, non-fatal exceptions at Error
    // level (see Flox.Shared.Errors.UiExceptionHandler) through the browser
    // console sink. Treating every console.error as fatal turned a correctly
    // handled exception (its own snackbar already shown) into this blocking
    // overlay too. If a host ever adds a real Blazor Server circuit, wire its
    // unhandled-exception callback to call window.showBlazorFatalError(...)
    // explicitly instead of resurrecting a blanket console.error hook.

    // Catches everything a targeted signal can: pre-boot failures and any other
    // truly uncaught error or rejection, on every platform this script runs on
    // (browser, or the WKWebView/Chromium WebView hosting a MAUI BlazorWebView).
    window.addEventListener('error', (e) => {
        const detail = e.error instanceof Error
            ? `${e.error.message}\n${e.error.stack ?? ''}`
            : (e.message || 'Unknown error');
        reportFatalError(detail);
    });

    window.addEventListener('unhandledrejection', (e) => {
        const reason = e.reason;
        const detail = reason instanceof Error
            ? `${reason.message}\n${reason.stack ?? ''}`
            : String(reason);
        reportFatalError(detail);
    });

    document.addEventListener('click', function (e) {
        if (e.target.classList.contains('blazor-error-details-toggle')) {
            toggleBlazorErrorStack(e.target);
        } else if (e.target.classList.contains('blazor-error-close')) {
            hide();
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && ui?.classList.contains('blazor-error-ui-active')) {
            hide();
        }
    });

    // Exposed so any other code (e.g. a .NET-side catch) can show the same fatal UI.
    window.showBlazorFatalError = reportFatalError;
})();
