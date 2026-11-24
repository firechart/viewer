// The SQLite of the browser client: sqlite-wasm in this dedicated worker over the VFS "OPFS SAHPool" (synchronous access handles exist only in a
// worker). BrowserSqliteEngine talks to it through flox-sqlite.js: a request is {id, op, args}, the answer {id, result} or {id, error, code}.
// Parameters, statements and rows cross as JSON text, which both sides read and write fast (the JSON of Blazor's interop is slow for thousands of
// values). The pool holds its files exclusively: only the leading tab of the pool runs this worker, the others ask it (flox-sqlite.js)
import sqlite3InitModule from './lib/sqlite-wasm/dist/index.mjs';

let sqlite3;
let pool;
let next = 0;
const handles = new Map();
// The bases being imported, each with the AbortController of its download: not there for anyone until complete
const importing = new Map();

// A new file needs a free handle of the pool, and the journal of a write another: two are kept free, the pool grows by itself
function reserve() {
    return pool.reserveMinimumCapacity(pool.getFileCount() + 2);
}

function busy(name) {
    if (importing.has(name)) {
        throw Object.assign(new Error(`The base ${name} is being downloaded.`), { resultCode: 5 });
    }
}

// Values across the boundary: a blob as {"b64"}, an integer beyond the safe range of JavaScript as {"i64"}
function fromWire(value) {
    if (value && typeof value === 'object') {
        if (typeof value.b64 === 'string') {
            return Uint8Array.from(atob(value.b64), c => c.charCodeAt(0));
        }
        if (typeof value.i64 === 'string') {
            return BigInt(value.i64);
        }
    }
    return value;
}

function toWire(key, value) {
    if (value instanceof Uint8Array) {
        let text = '';
        for (let i = 0; i < value.length; i += 0x8000) {
            text += String.fromCharCode.apply(null, value.subarray(i, i + 0x8000));
        }
        return { b64: btoa(text) };
    }
    if (typeof value === 'bigint') {
        return Number.isSafeInteger(Number(value)) ? Number(value) : { i64: value.toString() };
    }
    return value;
}

function bindValues(parameters) {
    if (!parameters) {
        return undefined;
    }
    const result = {};
    for (const [key, value] of Object.entries(parameters)) {
        result[key] = fromWire(value);
    }
    return result;
}

function bind(parametersJson) {
    return parametersJson ? bindValues(JSON.parse(parametersJson)) : undefined;
}

function fileName(name) {
    return '/' + name;
}

function database(handle) {
    const entry = handles.get(handle);
    if (!entry) {
        throw new Error(`No open base ${handle}.`);
    }
    return entry.db;
}

const operations = {
    async init(poolName, capacity) {
        if (!pool) {
            sqlite3 = await sqlite3InitModule();
            pool = await sqlite3.installOpfsSAHPoolVfs({ name: poolName, initialCapacity: capacity });
            await pool.reserveMinimumCapacity(Math.max(capacity, pool.getFileCount() + 2));
        }
        return sqlite3.version.libVersion;
    },

    async open(name, readOnly) {
        busy(name);
        if (!pool.getFileNames().includes(fileName(name))) {
            if (readOnly) {
                throw Object.assign(new Error(`The base ${name} does not exist.`), { resultCode: 14 });
            }
            await reserve();
            busy(name);
        }
        const db = new pool.OpfsSAHPoolDb(fileName(name));
        if (readOnly) {
            db.exec('PRAGMA query_only = 1');
        }
        const handle = ++next;
        handles.set(handle, { db, name });
        return handle;
    },

    // The rows as the UTF-8 bytes of the text {"columns":[...],"rows":[[...],...]}: bytes cross the interop of Blazor as they are, a string
    // would be escaped and parsed once more
    query(handle, sql, parametersJson) {
        const columns = [];
        const rows = database(handle).exec({ sql, bind: bind(parametersJson), rowMode: 'array', returnValue: 'resultRows', columnNames: columns });
        return new TextEncoder().encode(JSON.stringify({ columns, rows }, toWire));
    },

    execute(handle, sql, parametersJson) {
        const db = database(handle);
        db.exec({ sql, bind: bind(parametersJson) });
        return db.changes();
    },

    // statementsJson: [{"sql":"...","parameters":{...}},...], in one transaction
    batch(handle, statementsJson) {
        const db = database(handle);
        const changed = [];
        db.transaction(() => {
            for (const statement of JSON.parse(statementsJson)) {
                db.exec({ sql: statement.sql, bind: bindValues(statement.parameters) });
                changed.push(db.changes());
            }
        });
        return changed;
    },

    close(handle) {
        const entry = handles.get(handle);
        if (entry) {
            entry.db.close();
            handles.delete(handle);
        }
        return true;
    },

    exists(name) {
        return !importing.has(name) && pool.getFileNames().includes(fileName(name));
    },

    list() {
        return pool.getFileNames()
            .filter(file => !/-(journal|wal|shm)$/.test(file))
            .map(file => file.replace(/^\//, ''))
            .filter(name => !importing.has(name))
            .sort();
    },

    // A base compressed with gzip fetched and unpacked into the pool as it comes; a failure frees the file (importDb of SAHPool). The gzip stream
    // checks itself (CRC-32 and length): a cut or broken download fails. The progress is the compressed bytes received, posted every MB at most.
    // The file is named before the import: importDb takes a free handle of the pool without holding it, so a journal opened meanwhile by another
    // base (a write of flox.sqlite) would take the same handle and free it, base and all, when it is deleted. For the same reason the
    // name is held in importing from before the first await to the end: nobody sees, opens, removes or imports it meanwhile; cancelImport
    // stops the download. A tab closed in
    // the middle leaves a named file that does not open; the reader removes it
    async importGzip(id, name, url) {
        busy(name);
        if (pool.getFileNames().includes(fileName(name))) {
            throw Object.assign(new Error(`The base ${name} exists.`), { resultCode: 19 });
        }
        const cancel = new AbortController();
        importing.set(name, cancel);
        try {
            const response = await fetch(url, { cache: 'no-cache', signal: cancel.signal });
            if (!response.ok || !response.body) {
                throw Object.assign(new Error(`${url}: ${response.status} ${response.statusText}`), { resultCode: 14 });
            }
            await reserve();
            cancel.signal.throwIfAborted();
            new pool.OpfsSAHPoolDb(fileName(name)).close();
            let received = 0;
            let reported = 0;
            const counted = response.body.pipeThrough(new TransformStream({
                transform(chunk, controller) {
                    received += chunk.byteLength;
                    if (received - reported >= 1048576) {
                        reported = received;
                        postMessage({ id, progress: received });
                    }
                    controller.enqueue(chunk);
                }
            }));
            const reader = counted.pipeThrough(new DecompressionStream('gzip')).getReader();
            const written = await pool.importDb(fileName(name), async () => {
                const { done, value } = await reader.read();
                return done ? undefined : value;
            });
            postMessage({ id, progress: received });
            return written;
        } catch (error) {
            // importDb frees the file when it fails; whatever else left it named goes too
            if (pool.getFileNames().includes(fileName(name))) {
                pool.unlink(fileName(name));
            }
            if (cancel.signal.aborted) {
                throw Object.assign(new Error(`The download of ${name} was cancelled.`), { resultCode: 9 });
            }
            throw error;
        } finally {
            importing.delete(name);
        }
    },

    // Stops the download of a base being imported; its importGzip fails with the code 9 and leaves nothing
    cancelImport(name) {
        importing.get(name)?.abort();
        return true;
    },

    remove(name) {
        busy(name);
        for (const entry of handles.values()) {
            if (entry.name === name) {
                throw Object.assign(new Error(`The base ${name} is open.`), { resultCode: 5 });
            }
        }
        pool.unlink(fileName(name));
        return true;
    }
};

onmessage = async (event) => {
    const { id, op, args } = event.data;
    try {
        const result = op === 'importGzip' ? await operations.importGzip(id, ...args) : await operations[op](...(args ?? []));
        postMessage({ id, result }, result instanceof Uint8Array ? [result.buffer] : []);
    } catch (error) {
        postMessage({ id, error: String(error?.message ?? error), code: error?.resultCode ?? 0 });
    }
};
