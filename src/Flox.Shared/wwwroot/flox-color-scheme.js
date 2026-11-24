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
// application (the header .flox-app-bar is bg-background with the tokens theme.css gives it: dark coffee in the light theme), so the window and the
// bar are one surface. A probe reads it before the bar is rendered; a canvas turns it into sRGB, since not every browser takes oklch in theme-color
export function syncThemeColor() {
    const probe = document.createElement('div');
    probe.className = 'flox-app-bar';
    probe.hidden = true;
    document.body.appendChild(probe);
    const color = getComputedStyle(probe).getPropertyValue('--background').trim();
    probe.remove();
    if (!color) {
        return;
    }

    const context = document.createElement('canvas').getContext('2d');
    context.fillStyle = color;
    context.fillRect(0, 0, 1, 1);
    const [r, g, b] = context.getImageData(0, 0, 1, 1).data;

    let meta = document.querySelector('meta[name="theme-color"]');
    if (!meta) {
        meta = document.createElement('meta');
        meta.name = 'theme-color';
        document.head.appendChild(meta);
    }

    meta.content = `rgb(${r}, ${g}, ${b})`;
}
