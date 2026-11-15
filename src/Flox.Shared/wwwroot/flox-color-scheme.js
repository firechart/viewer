// The color scheme of the system (prefers-color-scheme) for the theme "System": its value now and every change, until stopped
const query = window.matchMedia('(prefers-color-scheme: dark)');
const watchers = new Map();

export function watch(id, dotnet) {
    const listener = event => dotnet.invokeMethodAsync('OnSystemDarkChanged', event.matches).catch(() => { });
    watchers.set(id, listener);
    query.addEventListener('change', listener);
    return query.matches;
}

export function stop(id) {
    const listener = watchers.get(id);
    if (listener) {
        query.removeEventListener('change', listener);
        watchers.delete(id);
    }
}

// The colour of the bar of the browser and of the installed window (meta theme-color) after a change of the theme: the background of the bar of the
// application (the header follows --rz-base-background-color of palette.css), so the window and the page are one surface
export function syncThemeColor() {
    const color = getComputedStyle(document.documentElement).getPropertyValue('--rz-base-background-color').trim();
    if (!color) {
        return;
    }

    let meta = document.querySelector('meta[name="theme-color"]');
    if (!meta) {
        meta = document.createElement('meta');
        meta.name = 'theme-color';
        document.head.appendChild(meta);
    }

    meta.content = color;
}
