// iOS WebKit (seen on an iPhone SE with iOS 27, not in the simulator) leaves out the text and the icons (the glyphs of Flox Symbols) of content
// that comes into a scroller or a floating layer: the options of a select, the icons of a menu and of the rows of a list stay empty until the
// next scroll. A layer given to their scroller for two frames a moment later paints them; a frame after they come is too early. Elsewhere nothing
// is installed
const delay = 300;

let pending = new Set();
let timer;

export function installIosRepaint() {
    if (!CSS.supports('-webkit-touch-callout', 'none') || typeof MutationObserver === 'undefined') {
        return;
    }
    new MutationObserver(onMutations).observe(document.body, { childList: true, subtree: true, characterData: true });
}

function onMutations(mutations) {
    for (const mutation of mutations) {
        pending.add(mutation.target.nodeType === Node.ELEMENT_NODE ? mutation.target : mutation.target.parentElement);
    }
    clearTimeout(timer);
    timer = setTimeout(repaint, delay);
}

function repaint() {
    const changed = pending;
    pending = new Set();
    const layers = new Set();
    for (const element of changed) {
        const layer = element?.isConnected ? layerOf(element) : null;
        // A transform of its own is left alone
        if (layer && !layer.style.transform) {
            layers.add(layer);
        }
    }
    for (const layer of layers) {
        layer.style.setProperty('transform', 'translateZ(0)');
    }
    requestAnimationFrame(() => requestAnimationFrame(() => {
        for (const layer of layers) {
            layer.style.removeProperty('transform');
        }
    }));
}

// The nearest scroller, or the content of a floating portal (Blazor Blueprint)
function layerOf(element) {
    for (let node = element; node && node !== document.body; node = node.parentElement) {
        if (node.parentElement?.hasAttribute('data-portal-content')) {
            return node;
        }
        const overflow = getComputedStyle(node).overflowY;
        if (overflow === 'auto' || overflow === 'scroll') {
            return node;
        }
    }
    return null;
}
