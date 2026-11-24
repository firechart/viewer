// The page's side of the SQLite worker (flox-sqlite-worker.js), imported by BrowserSqliteEngine. The pool of OPFS (SAHPool) holds its files
// exclusively, so the tabs and frames of one pool share one worker: the tab holding the Web Lock of the pool leads, runs the worker and answers the
// others over a BroadcastChannel; when it closes, the next waiting tab takes the lock and starts its own worker, and the requests left unanswered
// are sent to it again (so a write the old leader did without answering is done twice: at least once, not exactly once). A base open in a tab is a
// handle of that tab: the leader opens each base once for all the tabs, a connection for reading and one for writing (the worker runs one call at a
// time and every call is a whole transaction, so the two never see the file apart), and opens it again when the lead moves. The leader confirms
// each request as it gets it; a tab that has no confirmation in 10 s fails the request (the leader is paused in a debugger or hung) rather than
// wait for ever. A hidden leader gives the lead away, once it is idle, to a tab that waits for it: a phone suspends a hidden tab, lock and all.
// A worker that did not start is started again by the next request. Without Web Locks every tab leads (the old behaviour)
const tab = crypto.randomUUID();
const startAttempts = 20;
const startPause = 250;
// The leader confirms a request when it gets it; without that a tab gives up: the leader is paused in a debugger or hung, and it holds the lock
const acceptWait = 10000;

let poolName;
let poolCapacity;
let channel;
let started;
let leading = false;
// Ends the hold of the lock of the pool
let releaseLead;
// The requests the leader is answering now, and whether it gives the lead away when there is none
let serving = 0;
let yielding = false;

// The requests of this tab: id -> {op, args, resolve, reject, onProgress}
let nextRequest = 0;
const requests = new Map();

// The handles of this tab: handle -> {name, readOnly}
let nextHandle = 0;
const handles = new Map();

// The leader: its worker and the bases open in it, "name|readOnly" -> {handle (of the worker), users (the handles "tab:handle" of the tabs)};
// gone: the handles closed before the leader knew them (a call that was opening the base again must not bring them back)
let worker;
let workerReady;
let workerUp = false;
let nextCall = 0;
const calls = new Map();
const bases = new Map();
const gone = new Set();

export function init(pool, capacity) {
    // Once per page: start returns nothing, so the flag is set apart
    if (!started) {
        started = true;
        start(pool, capacity);
    } else if (pool !== poolName) {
        return Promise.reject(new Error(`[0] The page holds the pool ${poolName} of SQLite; the pool ${pool} cannot be opened beside it.`));
    }

    return request('version', []);
}

// Whether this tab runs the worker of the pool
export const leads = () => leading;

export async function open(name, readOnly) {
    const handle = ++nextHandle;
    await request('open', [handle, name, readOnly]);
    handles.set(handle, { name, readOnly });
    return handle;
}

export const query = (handle, sql, parameters) => request('query', [...base(handle), sql, parameters]);
export const execute = (handle, sql, parameters) => request('execute', [...base(handle), sql, parameters]);
export const batch = (handle, statements) => request('batch', [...base(handle), statements]);
export const exists = (name) => request('exists', [name]);
export const remove = (name) => request('remove', [name]);
export const list = () => request('list', []);
export const cancelImport = (name) => request('cancelImport', [name]);

export async function close(handle) {
    if (!handles.delete(handle)) {
        return true;
    }
    return request('close', [handle]);
}

// reporter: a DotNetObjectReference with Report(long). A base the user chose to download is worth keeping: the storage of the origin is asked to be
// persistent (without it the browser may evict OPFS under pressure); Firefox asks the user, the others decide by themselves; the answer changes nothing here
export async function importGzip(name, url, reporter) {
    const written = await request('importGzip', [name, url], received => reporter.invokeMethodAsync('Report', received).catch(() => { }));
    persist();
    return written;
}

// [handle, name, readOnly]: the leader opens the base again when the lead has moved
function base(handle) {
    const entry = handles.get(handle);
    if (!entry) {
        throw new Error(`[0] No open base ${handle}.`);
    }
    return [handle, entry.name, entry.readOnly];
}

function start(pool, capacity) {
    poolName = pool;
    poolCapacity = capacity;
    window.addEventListener('pagehide', onPageHide);
    window.addEventListener('pageshow', onPageShow);
    if (!navigator.locks || typeof BroadcastChannel === 'undefined') {
        becomeLeader();
        return;
    }

    channel = new BroadcastChannel(`flox-sqlite:${pool}`);
    channel.onmessage = event => onMessage(event.data);
    holdTab();
    waitForLead();
    document.addEventListener('visibilitychange', onVisibility);
}

// Held while the tab lives: the leader tells a closed tab from an open one by it. A page that leaves loses its locks (their promises are
// rejected, "the browsing context is going away"): one back from the back-forward cache takes it again
function holdTab() {
    navigator.locks.request(`flox-sqlite:${poolName}:tab:${tab}`, { ifAvailable: true }, lock => lock ? new Promise(() => { }) : undefined)
        .catch(() => { });
}

function waitForLead() {
    navigator.locks.request(`flox-sqlite:${poolName}`, () => {
        becomeLeader();
        channel.postMessage({ type: 'leader' });
        return new Promise(resolve => { releaseLead = resolve; });
    }).catch(() => { });
}

function becomeLeader() {
    leading = true;
    ready().catch(() => { });
    // What this tab asked the old leader is answered here now
    for (const [id, entry] of requests) {
        clearTimeout(entry.waiting);
        serve(tab, entry.op, entry.args, entry.onProgress).then(entry.resolve, error => entry.reject(asError(error))).finally(() => requests.delete(id));
    }
}

// A hidden leader with a tab waiting for the lead gives it away: the one the user looks at should not depend on a tab the system may suspend
async function onVisibility() {
    if (!document.hidden) {
        yielding = false;
        return;
    }
    if (!leading) {
        return;
    }
    const { pending } = await navigator.locks.query();
    if (document.hidden && pending.some(lock => lock.name === `flox-sqlite:${poolName}`)) {
        yielding = true;
        stepDown();
    }
}

// A page that goes into the back-forward cache (Safari) keeps its worker, and the worker the files of the pool: the next page of the tab gets the
// lock but cannot open them. A page that leaves stops its worker and gives the lead away; one that comes back from the cache waits for it again
function onPageHide() {
    if (!leading) {
        return;
    }
    leading = false;
    yielding = false;
    workerUp = false;
    worker?.terminate();
    worker = undefined;
    workerReady = undefined;
    // Not answered to the other tabs: they send their requests again to the next leader
    for (const entry of calls.values()) {
        entry.reject({ message: 'The page was left.', code: 5, left: true });
    }
    calls.clear();
    bases.clear();
    gone.clear();
    const release = releaseLead;
    releaseLead = undefined;
    release?.();
}

function onPageShow(event) {
    if (!event.persisted || leading) {
        return;
    }
    if (channel) {
        holdTab();
        waitForLead();
    } else {
        becomeLeader();
    }
}

// Only between requests and with a worker that runs: nothing is cut in the middle. The tab waits for the lead again, behind the others
function stepDown() {
    if (!yielding || !leading || !releaseLead || !workerUp || serving > 0 || !document.hidden) {
        return;
    }
    yielding = false;
    leading = false;
    workerUp = false;
    worker.terminate();
    worker = undefined;
    workerReady = undefined;
    calls.clear();
    bases.clear();
    gone.clear();
    const release = releaseLead;
    releaseLead = undefined;
    release();
    waitForLead();
}

function request(op, args, onProgress) {
    return new Promise((resolve, reject) => {
        if (leading) {
            serve(tab, op, args, onProgress).then(resolve, error => reject(asError(error)));
            return;
        }

        const id = ++nextRequest;
        const entry = { op, args, resolve, reject, onProgress };
        requests.set(id, entry);
        send(id, entry);
    });
}

function send(id, entry) {
    clearTimeout(entry.waiting);
    entry.waiting = setTimeout(() => {
        if (requests.delete(id)) {
            entry.reject(new Error('[5] The tab that holds the bases of the device does not answer (paused in a debugger or hung): close it or reload it.'));
        }
    }, acceptWait);
    channel.postMessage({ type: 'request', from: tab, id, op: entry.op, args: entry.args, visible: !document.hidden });
}

function onMessage(message) {
    switch (message.type) {
        case 'request':
            if (leading) {
                const { from, id, op, args } = message;
                channel.postMessage({ type: 'accepted', to: from, id });
                // Asked by a tab the user looks at while this one is hidden: answered, then the lead goes
                if (message.visible && document.hidden) {
                    yielding = true;
                }
                serve(from, op, args, progress => channel.postMessage({ type: 'progress', to: from, id, progress }))
                    .then(result => channel.postMessage({ type: 'reply', to: from, id, result }),
                        error => error?.left || channel.postMessage({ type: 'reply', to: from, id, error: error.message, code: error.code }));
            }
            break;
        case 'accepted':
            if (message.to === tab) {
                clearTimeout(requests.get(message.id)?.waiting);
            }
            break;
        case 'reply': {
            const entry = message.to === tab && !leading ? requests.get(message.id) : undefined;
            if (entry) {
                clearTimeout(entry.waiting);
                requests.delete(message.id);
                if (message.error !== undefined) {
                    entry.reject(asError(message));
                } else {
                    entry.resolve(message.result);
                }
            }
            break;
        }
        case 'progress':
            if (message.to === tab) {
                requests.get(message.id)?.onProgress?.(message.progress);
            }
            break;
        case 'leader':
            // A new leader: the requests the old one left unanswered go to it
            if (!leading) {
                for (const [id, entry] of requests) {
                    send(id, entry);
                }
            }
            break;
    }
}

// The leader answers a request of a tab (its own included); a failure is {message, code}
async function serve(from, op, args, onProgress) {
    serving++;
    try {
        return await answer(from, op, args, onProgress);
    } finally {
        serving--;
        stepDown();
    }
}

async function answer(from, op, args, onProgress) {
    const version = await ready();
    switch (op) {
        case 'version':
            return version;
        case 'open': {
            const [handle, name, readOnly] = args;
            await acquire(from, handle, name, readOnly);
            return true;
        }
        case 'query':
        case 'execute':
        case 'batch': {
            const [handle, name, readOnly, ...rest] = args;
            const opened = await acquire(from, handle, name, readOnly);
            return call(op, [opened, ...rest]);
        }
        case 'close':
            await release(`${from}:${args[0]}`);
            return true;
        case 'remove':
            await forgetClosedTabs();
            // The browser frees the lock of a tab a moment after it closes
            if ([...bases].some(([key, entry]) => key.startsWith(`${args[0]}|`) && entry.users.size > 0)) {
                await new Promise(resolve => setTimeout(resolve, startPause));
                await forgetClosedTabs();
            }
            for (const [key, entry] of [...bases]) {
                if (key.startsWith(`${args[0]}|`)) {
                    if (entry.users.size > 0 || entry.handle === undefined) {
                        throw { message: `The base ${args[0]} is open.`, code: 5 };
                    }
                    // Left open by a call that came after its handle was closed
                    bases.delete(key);
                    await call('close', [entry.handle]);
                }
            }
            return call('remove', args);
        case 'importGzip':
            return call('importGzip', args, onProgress);
        default:
            return call(op, args);
    }
}

// The worker of the leader, started once; one that failed is started again by the next request
function ready() {
    workerReady ??= startWorker().then(version => {
        workerUp = true;
        return version;
    }, error => {
        workerReady = undefined;
        throw error;
    });
    return workerReady;
}

// The connection of the worker to the base, opened once for every tab that uses it
async function acquire(from, handle, name, readOnly) {
    const key = `${name}|${readOnly}`;
    const user = `${from}:${handle}`;
    let entry = bases.get(key);
    if (!entry) {
        entry = { opening: call('open', [name, readOnly]), users: new Set() };
        bases.set(key, entry);
        try {
            entry.handle = await entry.opening;
        } catch (error) {
            bases.delete(key);
            throw error;
        }
    } else if (entry.handle === undefined) {
        await entry.opening;
    }
    if (!gone.delete(user)) {
        entry.users.add(user);
    }
    return entry.handle;
}

async function release(user) {
    let known = false;
    for (const [key, entry] of bases) {
        if (entry.users.delete(user)) {
            known = true;
            if (entry.users.size === 0 && entry.handle !== undefined) {
                bases.delete(key);
                await call('close', [entry.handle]);
            }
        }
    }
    // Closed while a call of the same handle is still opening the base here (the lead has moved)
    if (!known && [...bases.values()].some(entry => entry.handle === undefined)) {
        gone.add(user);
    }
}

// The handles of a tab that closed without closing them
async function forgetClosedTabs() {
    if (!navigator.locks) {
        return;
    }
    const { held } = await navigator.locks.query();
    const open = new Set(held.map(lock => lock.name));
    const users = [...bases.values()].flatMap(entry => [...entry.users]);
    for (const user of users) {
        const owner = user.slice(0, user.lastIndexOf(':'));
        if (owner !== tab && !open.has(`flox-sqlite:${poolName}:tab:${owner}`)) {
            await release(user);
        }
    }
}

// The worker of the leader; the files of the pool may still be held for a moment by the worker of the tab that led before: a new worker each
// attempt. A browser that gives no OPFS (a private window, an insecure page) is told at once, not after the attempts
async function startWorker() {
    try {
        await navigator.storage.getDirectory();
    } catch (error) {
        throw { message: `The browser gives this page no private file system (OPFS), so the data of the device cannot be kept: ${error?.message ?? error}`, code: 14 };
    }
    for (let attempt = 1; ; attempt++) {
        createWorker();
        try {
            return await call('init', [poolName, poolCapacity]);
        } catch (error) {
            worker.terminate();
            if (attempt >= startAttempts) {
                throw error;
            }
            await new Promise(resolve => setTimeout(resolve, startPause));
        }
    }
}

function createWorker() {
    worker = new Worker(new URL('./flox-sqlite-worker.js', import.meta.url), { type: 'module' });
    worker.onmessage = event => {
        const { id, result, error, code, progress } = event.data;
        const entry = calls.get(id);
        if (progress !== undefined) {
            entry?.onProgress?.(progress);
            return;
        }
        calls.delete(id);
        if (error !== undefined) {
            entry?.reject({ message: error, code });
        } else {
            entry?.resolve(result);
        }
    };
    worker.onerror = event => {
        for (const entry of calls.values()) {
            entry.reject({ message: `The SQLite worker failed: ${event.message}`, code: 0 });
        }
        calls.clear();
    };
}

function call(op, args, onProgress) {
    return new Promise((resolve, reject) => {
        const id = ++nextCall;
        calls.set(id, { resolve, reject, onProgress });
        worker.postMessage({ id, op, args });
    });
}

// The code of SQLite travels in the message: the .NET side reads "[code] message"
function asError(error) {
    return error instanceof Error ? error : new Error(`[${error?.code ?? 0}] ${error?.message ?? error?.error ?? error}`);
}

function persist() {
    if (navigator.storage?.persist) {
        navigator.storage.persisted()
            .then(already => already || navigator.storage.persist())
            .catch(() => { });
    }
}
