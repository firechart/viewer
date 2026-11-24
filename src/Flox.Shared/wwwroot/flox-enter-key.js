// On a touch screen the Enter key of the on-screen keyboard moves through a form as in a native one: Next on a field with another field after it in
// the same form or dialog, Done on the last one, which takes the focus away and so closes the keyboard (Android has no other key for it). A field
// that names its own key (enterkeyhint: the search) or needs Enter itself (a textarea, a combobox) is left alone, and so is a mouse with a keyboard
const textField = 'input:not([type]), input[type=text], input[type=search], input[type=email], input[type=url], input[type=tel], input[type=number], input[type=password]';
const touch = window.matchMedia('(pointer: coarse)');
let installed = false;

function isField(element) {
    return element instanceof HTMLElement
        && (element.matches(textField) || element.matches('textarea'))
        && element.getAttribute('role') !== 'combobox'
        && !element.hasAttribute('aria-autocomplete')
        && !element.disabled
        && !element.readOnly
        && element.getClientRects().length > 0;
}

function nextField(input) {
    const scope = input.closest('form, [role=dialog], [role=alertdialog]') ?? input.closest('main') ?? document.body;
    const fields = [...scope.querySelectorAll(`${textField}, textarea`)].filter(isField);
    return fields[fields.indexOf(input) + 1] ?? null;
}

function owned(input) {
    return input.matches(textField) && isField(input) && (input.dataset.floxEnter !== undefined || !input.hasAttribute('enterkeyhint'));
}

function onFocusIn(event) {
    const input = event.target;
    if (!touch.matches || !owned(input)) {
        return;
    }

    const key = nextField(input) ? 'next' : 'done';
    input.dataset.floxEnter = key;
    input.setAttribute('enterkeyhint', key);
}

function onKeyDown(event) {
    const input = event.target;
    if (event.key !== 'Enter' || event.isComposing || !touch.matches || !(input instanceof HTMLElement) || input.dataset.floxEnter === undefined) {
        return;
    }

    event.preventDefault();
    const next = nextField(input);
    if (next) {
        next.focus();
    } else {
        input.blur();
    }
}

export function installEnterKey() {
    if (installed) {
        return;
    }

    installed = true;
    document.addEventListener('focusin', onFocusIn);
    document.addEventListener('keydown', onKeyDown, true);
}
