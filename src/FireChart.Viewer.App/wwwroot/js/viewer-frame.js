// The frame side of the messages between /embed and the page that embeds it (the SDK, the sandbox). ?host=parent marks the frame; the mark stays in
// the session storage of the tab, so the way back of a SMART redirect asks the parent again. Only a parent whose origin is in host.json (the
// frame-ancestors of the hosting: 'self', https://emr.example.org, https://*.example.org, *) may drive it; the events go to that origin only
const protocol = "fxv";
const mark = "fxv.host";
let dotnet = null;
let allowed = [];
let parentOrigin = null;
let resizeObserver = null;
let lastHeight = 0;

export function isParentHosted() {
    if (window.parent === window) {
        return false;
    }

    try {
        if (new URL(window.location.href).searchParams.get("host") === "parent") {
            sessionStorage.setItem(mark, "parent");
        }

        return sessionStorage.getItem(mark) === "parent";
    } catch {
        return false;
    }
}

async function loadAllowed() {
    try {
        const response = await fetch("host.json", { cache: "no-cache" });
        const host = response.ok ? await response.json() : {};
        return Array.isArray(host.frameAncestors) ? host.frameAncestors.map(String) : ["'self'"];
    } catch {
        return ["'self'"];
    }
}

function matches(source, origin) {
    if (source === "*") {
        return true;
    }

    if (source === "'self'") {
        return origin === window.location.origin;
    }

    if (source === "'none'") {
        return false;
    }

    const wildcard = /^(https?):\/\/\*\.(.+)$/.exec(source);
    if (wildcard) {
        try {
            const url = new URL(origin);
            return url.protocol === wildcard[1] + ":" && url.host.endsWith("." + wildcard[2]);
        } catch {
            return false;
        }
    }

    return source.replace(/\/$/, "") === origin;
}

function onMessage(event) {
    if (event.source !== window.parent || !allowed.some(source => matches(source, event.origin))) {
        return;
    }

    const message = event.data;
    if (!message || message.protocol !== protocol || typeof message.type !== "string") {
        return;
    }

    const first = parentOrigin === null;
    parentOrigin = event.origin;
    if (first) {
        sendHeight();
    }

    // Printing needs no .NET: the print dialog of the browser for this document
    if (message.type === "print") {
        window.print();
        if (typeof message.id === "string") {
            post("result", message.id, JSON.stringify({ ok: true, value: {} }));
        }

        return;
    }

    dotnet?.invokeMethodAsync("OnMessage", message.type, typeof message.id === "string" ? message.id : null, JSON.stringify(message.payload ?? {}));
}

// The height of the content (the body, not the document: the document is at least as high as the frame and would never shrink), for the
// automatic height of the SDK
function sendHeight() {
    const height = Math.ceil(document.body.scrollHeight);
    if (height !== lastHeight && parentOrigin) {
        lastHeight = height;
        window.parent.postMessage({ protocol, type: "resize", payload: { height } }, parentOrigin);
    }
}

function observeHeight() {
    resizeObserver = new ResizeObserver(sendHeight);
    resizeObserver.observe(document.body);
}

export async function connect(reference) {
    dotnet = reference;
    allowed = await loadAllowed();
    window.addEventListener("message", onMessage);
    observeHeight();
    // hello carries nothing: any parent may learn that the frame is there, only an allowed one gets an answer
    window.parent.postMessage({ protocol, type: "hello", payload: {} }, "*");
}

// The events and the results go only to the origin that drives the frame; none before its first message
export function post(type, id, payload) {
    if (!parentOrigin) {
        return;
    }

    const message = { protocol, type, payload: JSON.parse(payload) };
    if (id) {
        message.id = id;
    }

    window.parent.postMessage(message, parentOrigin);
}

export function disconnect() {
    window.removeEventListener("message", onMessage);
    resizeObserver?.disconnect();
    dotnet = null;
}
