let _sharing = false;

async function copyToClipboard(text) {
    if (navigator.clipboard) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // without the focus of the document or a user gesture (lost on the way through Blazor): the fallback below
        }
    }
    // Legacy fallback: works without document focus or user gesture; the focus goes back where it was
    const active = document.activeElement;
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.setAttribute('readonly', '');
    textarea.style.cssText = 'position:fixed;left:-9999px;top:-9999px;opacity:0;';
    document.body.appendChild(textarea);
    try {
        textarea.focus();
        textarea.select();
        return document.execCommand('copy');
    } finally {
        document.body.removeChild(textarea);
        if (active instanceof HTMLElement) {
            active.focus({ preventScroll: true });
        }
    }
}

export async function share(title, text) {
    // navigator.share requires a user gesture that Blazor WASM's async bridge
    // consistently breaks on desktop — the Promise hangs forever.
    // Restrict native share to touch devices where it actually works.
    const isTouchDevice = navigator.maxTouchPoints > 0;
    if (isTouchDevice && navigator.share && !_sharing) {
        _sharing = true;
        try {
            await navigator.share({ title, text });
            return 'shared';
        } catch (e) {
            if (e.name === 'AbortError') return 'cancelled';
            // NotAllowedError, InvalidStateError — fall through to clipboard
        } finally {
            _sharing = false;
        }
    }
    const copied = await copyToClipboard(text);
    return copied ? 'copied' : 'unsupported';
}

export function canShare() {
    return !!(navigator.share || navigator.clipboard || document.execCommand);
}
