// The caching service worker of a Flox application that works offline (FireChart Lite), shared: the worker of the application imports its manifest
// (service-worker-assets.js of the build) and this file, and calls floxCachingWorker with what it caches.
// - Install: a version is cached file by file; a file of the version before with the same integrity (kept in its cache with the header below) is
//   taken from there, the others are downloaded with their integrity; every tab hears how far it is (a message 'app-update' of kind 'cache': the
//   files loaded of the files to load, update, done, failed), which flox-update.js shows.
// - The new version waits until every tab is closed, or takes over when a tab asks (Restart, 'app-update-apply'); it tells the tabs first
//   ('app-update-applying'), so they reload into it.
// - Fetch: a page of the application is its page (index.html); a file of the manifest is found whatever its query (a sheet asked for as
//   <name>.css?v=<version>); anything else goes to the network.
self.floxCachingWorker = options => {
    const cacheNamePrefix = options.cachePrefix;
    const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
    const include = options.include;
    const exclude = options.exclude || [];
    const integrityHeader = 'x-flox-integrity';
    const parallelDownloads = 6;

    // The scope of the worker is the root of the application; the addresses of the manifest are relative to it
    const scope = new URL(self.registration.scope);
    const pageUrl = new URL(options.page || 'index.html', scope).href;
    const assets = self.assetsManifest.assets;
    const manifestUrls = new Set(assets.map(asset => new URL(asset.url, scope).href));
    let progress = null;
    let reportedAt = 0;

    self.addEventListener('install', event => event.waitUntil(onInstall()));
    self.addEventListener('activate', event => event.waitUntil(onActivate()));
    self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
    self.addEventListener('message', event => onMessage(event));

    async function onInstall() {
        const wanted = assets
            .filter(asset => include.some(pattern => pattern.test(asset.url)))
            .filter(asset => !exclude.some(pattern => pattern.test(asset.url)));
        const cache = await caches.open(cacheName);
        const previous = await previousCaches();
        const missing = [];
        for (const asset of wanted) {
            const url = new URL(asset.url, scope).href;
            const kept = await findKept(previous, url, asset.hash);
            if (kept) {
                await cache.put(url, kept);
            } else {
                missing.push(asset);
            }
        }

        progress = { kind: 'cache', update: Boolean(self.registration.active), loaded: 0, total: missing.length, done: false, failed: false };
        report(true);
        try {
            const queue = missing.slice();
            const download = async () => {
                for (let asset = queue.shift(); asset; asset = queue.shift()) {
                    await cacheAsset(cache, asset);
                }
            };
            await Promise.all(Array.from({ length: parallelDownloads }, download));
            progress.done = true;
        } catch (error) {
            progress.failed = true;
            throw error;
        } finally {
            report(true);
        }
    }

    async function onActivate() {
        const keys = await caches.keys();
        await Promise.all(keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName).map(key => caches.delete(key)));
    }

    async function onFetch(event) {
        if (event.request.method === 'GET') {
            const url = new URL(event.request.url);
            const path = url.origin + url.pathname;
            const isPage = event.request.mode === 'navigate' && !manifestUrls.has(path);
            const cache = await caches.open(cacheName);
            const cached = await cache.match(isPage ? pageUrl : event.request, { ignoreSearch: !isPage && manifestUrls.has(path) });
            if (cached) {
                return cached;
            }
        }

        return fetch(event.request);
    }

    // A tab asks how far the install is (it opened during it), or asks the waiting version to take over (Restart)
    async function onMessage(event) {
        if (event.data?.type === 'app-update-status' && progress) {
            event.source?.postMessage({ type: 'app-update', ...progress });
        } else if (event.data?.type === 'app-update-apply') {
            await toTabs({ type: 'app-update-applying' });
            self.skipWaiting();
        }
    }

    // The caches of the versions before (a file of the same integrity is the same file)
    async function previousCaches() {
        const keys = await caches.keys();
        return Promise.all(keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName).map(key => caches.open(key)));
    }

    async function findKept(previous, url, integrity) {
        for (const cache of previous) {
            const response = await cache.match(url);
            if (response && response.headers.get(integrityHeader) === integrity) {
                return response;
            }
        }

        return null;
    }

    // The file downloaded with its integrity checked by the browser, kept with its integrity for the next version
    async function cacheAsset(cache, asset) {
        const url = new URL(asset.url, scope).href;
        const response = await fetch(new Request(url, { integrity: asset.hash, cache: 'no-cache' }));
        if (!response.ok) {
            throw new Error(`${asset.url}: ${response.status}`);
        }

        // The body is decoded: the encoding and the length the network had no longer describe it
        const headers = new Headers(response.headers);
        headers.delete('content-encoding');
        headers.delete('content-length');
        headers.set(integrityHeader, asset.hash);
        await cache.put(url, new Response(await response.blob(), { status: response.status, statusText: response.statusText, headers }));
        progress.loaded++;
        report(false);
    }

    // To every tab, at most a few times a second unless it is the start or the end
    function report(now) {
        if (!progress || (!now && Date.now() - reportedAt < 200)) {
            return;
        }

        reportedAt = Date.now();
        toTabs({ type: 'app-update', ...progress });
    }

    async function toTabs(message) {
        const tabs = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        tabs.forEach(tab => tab.postMessage(message));
    }
};
