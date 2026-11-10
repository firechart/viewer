// The documents of IndexedDbDocumentStore: one object store, the key is [collection, key]
const DATABASE = 'flox';
const STORE = 'documents';
const VERSION = 1;

let opening;

function open() {
    opening ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(DATABASE, VERSION);
        request.onupgradeneeded = () => {
            const store = request.result.createObjectStore(STORE, { keyPath: ['c', 'k'] });
            store.createIndex('c', 'c');
        };
        request.onsuccess = () => {
            const db = request.result;
            // A newer version in another tab: let it upgrade, open again on the next call
            db.onversionchange = () => {
                db.close();
                opening = undefined;
            };
            resolve(db);
        };
        request.onerror = () => {
            opening = undefined;
            reject(request.error);
        };
        request.onblocked = () => {
            opening = undefined;
            reject(new Error('The local database is open in another tab with an older version: close the other tabs.'));
        };
    });
    return opening;
}

function completed(transaction) {
    return new Promise((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error ?? new Error('The transaction of the local database was aborted.'));
    });
}

export async function load(collection) {
    const db = await open();
    const transaction = db.transaction(STORE, 'readonly');
    const request = transaction.objectStore(STORE).index('c').getAll(collection);
    const rows = await new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    return rows.map(row => ({ key: row.k, json: row.j }));
}

export async function put(collection, documents) {
    const db = await open();
    const transaction = db.transaction(STORE, 'readwrite');
    const store = transaction.objectStore(STORE);
    for (const document of documents) {
        store.put({ c: collection, k: document.key, j: document.json });
    }
    await completed(transaction);
}

export async function remove(collection, keys) {
    const db = await open();
    const transaction = db.transaction(STORE, 'readwrite');
    const store = transaction.objectStore(STORE);
    for (const key of keys) {
        store.delete([collection, key]);
    }
    await completed(transaction);
}
