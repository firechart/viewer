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

// The sharing dialog of the system for a text (Web Share): Safari, Chrome and Edge on Android, Windows, macOS and ChromeOS; not Firefox on a desktop
export function canShare() {
    return typeof navigator.share === 'function' && (typeof navigator.canShare !== 'function' || navigator.canShare({ text: 'x' }));
}

// Opens the sharing dialog; it needs the click of the user, so the module is loaded before (ShareService.PrepareAsync)
export async function share(title, text) {
    if (!canShare()) {
        return 'unsupported';
    }
    if (_sharing) {
        return 'cancelled';
    }
    _sharing = true;
    try {
        await navigator.share({ title, text });
        return 'shared';
    } catch (e) {
        // AbortError: the user closed the dialog; NotAllowedError: no click of the user, or the dialog is not allowed here
        return e.name === 'AbortError' ? 'cancelled' : 'failed';
    } finally {
        _sharing = false;
    }
}

// Copies the text to the clipboard: true when it was copied
export async function copy(text) {
    return await copyToClipboard(text);
}
