// The SMART sign-in in a popup (SmartFlow.Popup): the window is opened at the click (a popup blocker lets only that through), the address of the
// authorization server is set when it is known, and the return page (smart-callback.html, the same origin as this document) posts the address of the
// way back here. The code is exchanged by the caller, in this document, where the PKCE secret is.
const MessageType = "firechart-smart-callback";
let popup = null;

// false: the browser blocked the window (no click, a blocker, a sandboxed frame without allow-popups)
export function open() {
    popup = window.open("about:blank", "firechart-smart", "popup,width=520,height=720");
    return !!popup;
}

export function go(url) {
    if (popup && !popup.closed) {
        popup.location.href = url;
    }
}

export function close() {
    try {
        if (popup && !popup.closed) {
            popup.close();
        }
    } catch {
        // a window the opener lost
    }
}

// { url } when the return page posted the way back; { closed: true } when the window was closed, or the opener lost it (Cross-Origin-Opener-Policy of
// a page of the authorization server: the window then looks closed); { timeout: true } after timeoutMs
export function wait(timeoutMs) {
    return new Promise(resolve => {
        let timer = 0;
        let limit = 0;
        const done = result => {
            window.removeEventListener("message", onMessage);
            clearInterval(timer);
            clearTimeout(limit);
            resolve(result);
        };
        const onMessage = event => {
            if (event.origin !== location.origin || !event.data || event.data.type !== MessageType || typeof event.data.url !== "string") {
                return;
            }

            done({ url: event.data.url });
        };
        window.addEventListener("message", onMessage);
        timer = setInterval(() => {
            if (!popup || popup.closed) {
                done({ closed: true });
            }
        }, 500);
        limit = setTimeout(() => done({ timeout: true }), timeoutMs);
    });
}
