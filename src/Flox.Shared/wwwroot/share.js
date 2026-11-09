let _sharing = false;

async function copyToClipboard(text) {
    if (navigator.clipboard && document.hasFocus()) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // fall through to execCommand
        }
    }
    // Legacy fallback: works without document focus or user gesture
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.cssText = 'position:fixed;left:-9999px;top:-9999px;opacity:0;';
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(textarea);
    return ok;
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
