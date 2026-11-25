// The update of a Flox application (AppUpdateBar): what its service worker says about a new version, a check of the server from time to time, and
// Restart. Two kinds of workers speak to it (a message 'app-update'):
// - 'cache' (flox-service-worker.js, an application that works offline): the files of a new version as they download, then the version waits;
//   Restart asks it to take over, and the tabs reload into it;
// - 'online' (the worker a Flox server gives its clients): it caches nothing and takes over at once; it names the
//   version of the server, and a version other than the one this page started with is a new version: Restart reloads the page.
// The browser looks for a new worker only when a page loads; an installed application on a phone wakes from memory instead, so the page asks the
// server when it comes back on screen (at most every five minutes) and every hour while it is open (registration.update(): the worker of the server
// compared byte for byte with its own). A tab reloads only when Restart asked for it ('app-update-applying'), never in the middle of a form.
let listening = false;
let checkedAt = 0;
let applying = false;
let startedWith = null;
const checkEvery = 5 * 60 * 1000;
const checkWhileOpen = 60 * 60 * 1000;

async function check(force) {
    if (!force && Date.now() - checkedAt < checkEvery) {
        return;
    }

    checkedAt = Date.now();
    try {
        const registration = await navigator.serviceWorker.getRegistration();
        await registration?.update();
    } catch {
        // Offline or the server does not answer: the next check asks again
    }
}

// The version the active worker names (an online worker), once a worker the load of this page found has taken over
async function versionOfActive() {
    const registration = await navigator.serviceWorker.ready;
    for (let i = 0; i < 50 && registration.installing; i++) {
        await new Promise(resolve => setTimeout(resolve, 200));
    }

    const active = registration.active;
    if (!active) {
        return null;
    }

    return new Promise(resolve => {
        const channel = new MessageChannel();
        const timer = setTimeout(() => resolve(null), 2000);
        channel.port1.onmessage = event => {
            clearTimeout(timer);
            resolve(event.data?.version ?? null);
        };
        active.postMessage({ type: 'app-update-version' }, [channel.port2]);
    });
}

export function watch(component) {
    if (!('serviceWorker' in navigator) || listening) {
        return;
    }

    listening = true;
    const started = versionOfActive().then(version => startedWith = version).catch(() => null);
    navigator.serviceWorker.addEventListener('message', async event => {
        const message = event.data;
        if (message?.type === 'app-update-applying') {
            applying = true;
        } else if (message?.type === 'app-update' && message.kind === 'online') {
            await started;
            if (message.version && startedWith && message.version !== startedWith) {
                component.invokeMethodAsync('Progress', { kind: 'online', update: true, loaded: 0, total: 0, done: true, failed: false });
            }
        } else if (message?.type === 'app-update') {
            component.invokeMethodAsync('Progress', message);
        }
    });
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (applying) {
            location.reload();
        }
    });
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            check(false);
        }
    });
    setInterval(() => check(true), checkWhileOpen);
    navigator.serviceWorker.getRegistration().then(registration => {
        // A version downloaded earlier and waiting (the tabs were not closed since)
        if (registration?.waiting && registration.active) {
            component.invokeMethodAsync('Progress', { kind: 'cache', update: true, loaded: 0, total: 0, done: true, failed: false });
        }

        (registration?.installing ?? registration?.waiting)?.postMessage({ type: 'app-update-status' });
    }).catch(() => { });
}

// A waiting version takes over and this tab reloads when it is active (also a tab no worker controlled yet, opened before the first install); without
// one (an online worker took over already) the page reloads
export async function apply() {
    applying = true;
    const waiting = (await navigator.serviceWorker?.getRegistration())?.waiting;
    if (!waiting) {
        location.reload();
        return;
    }

    waiting.addEventListener('statechange', () => {
        if (waiting.state === 'activated') {
            location.reload();
        }
    });
    waiting.postMessage({ type: 'app-update-apply' });
}
