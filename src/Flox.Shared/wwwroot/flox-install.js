// The offer of the browser to install the application (Chromium: beforeinstallprompt). It comes once, often before Blazor has started, so the page
// keeps it from its head for the component that asks (Components/AppInstall.razor.js). No offer: the platform cannot install (Safari, Firefox) or the
// application is installed already. preventDefault is not called: the browser keeps its own offer (the bar of Chrome on Android)
(() => {
    const install = { offer: null, listeners: new Set() };
    install.changed = () => install.listeners.forEach(listener => listener());
    addEventListener('beforeinstallprompt', event => {
        install.offer = event;
        install.changed();
    });
    addEventListener('appinstalled', () => {
        install.offer = null;
        install.changed();
    });
    window.floxInstall = install;
})();
