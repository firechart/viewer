// The sandbox side of the messages with the frame of the Viewer (same origin): what the frame says goes to .NET, the commands go to the frame
// The frame is created when a server is chosen and removed when none is: it is looked up in its host at every message
const protocol = "fxv";
let host = null;
let dotnet = null;

function frame() {
    return host?.querySelector("iframe") ?? null;
}

function onMessage(event) {
    const current = frame();
    if (!current || event.source !== current.contentWindow || event.origin !== window.location.origin) {
        return;
    }

    const message = event.data;
    if (!message || message.protocol !== protocol || typeof message.type !== "string") {
        return;
    }

    dotnet?.invokeMethodAsync("OnFrameMessage", message.type, JSON.stringify(message.payload ?? {}));
}

export function attach(element, reference) {
    host = element;
    dotnet = reference;
    window.addEventListener("message", onMessage);
}

export function send(type, payload) {
    frame()?.contentWindow?.postMessage({ protocol, type, payload: JSON.parse(payload) }, window.location.origin);
}

export function copy(text) {
    return navigator.clipboard.writeText(text);
}

export function detach() {
    window.removeEventListener("message", onMessage);
    host = null;
    dotnet = null;
}
