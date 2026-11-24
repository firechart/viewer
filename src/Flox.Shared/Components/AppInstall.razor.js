// Whether the browser offers to install the application (the offer kept by flox-install.js in the head of the page) and the installation itself
export function watch(dotnet) {
    const state = window.floxInstall;
    if (!state) {
        return null;
    }

    let shown = null;
    const listener = () => {
        const available = state.offer !== null;
        if (available !== shown) {
            shown = available;
            dotnet.invokeMethodAsync('SetAvailable', available);
        }
    };
    state.listeners.add(listener);
    listener();
    return {
        dispose() {
            state.listeners.delete(listener);
        }
    };
}

// An offer is shown once: whatever the user answers, it is gone (the browser may offer again later)
export async function install() {
    const state = window.floxInstall;
    const offer = state?.offer;
    if (!offer) {
        return;
    }

    state.offer = null;
    try {
        await offer.prompt();
        await offer.userChoice;
    } catch {
        // the offer is no longer valid
    }

    state.changed();
}
