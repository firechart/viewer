// FireChart Viewer SDK: the Viewer in an iframe of its own origin (FireChart.Viewer.App at /embed), driven by postMessage.
// Hand-written ESM, no build, no dependencies. Licensed AGPL-3.0-only or under a commercial license of the owner.

const PROTOCOL = "fxv";
const EVENTS = ["ready", "navigate", "restarting", "updated", "error"];
// The keys that change without a restart (live and reload); every other key needs one
const LIVE = ["locale", "appearance", "terminology", "clinicalSummary"];
// The languages of the texts of the Viewer (Locales of ViewerAppConfigLoader); another language of the host is "auto"
const LOCALES = ["en", "de", "es", "ru", "zh-Hans"];
const INHERIT = "inherit";

/** An error of the Viewer: a configuration it cannot use, an address that is not its own, a destroyed instance. */
export class ViewerError extends Error {
    constructor(error) {
        super(error.message);
        this.name = "ViewerError";
        this.type = error.type;
        this.code = error.code;
        this.param = error.param;
    }
}

/** One Viewer in one iframe. Create it with Viewer.create(config, options), then mount it. */
export class Viewer {
    #url;
    #origin;
    #config;
    #height;
    #title;
    #frame = null;
    #listener = null;
    #handlers = new Map();
    #pending = new Map();
    #queue = [];
    #connected = false;
    #location = null;
    #sequence = 0;
    #urlParam;
    #deepLink = null;
    #observer = null;
    #inherited = null;

    /**
     * @param {object} config the configuration of the Viewer, as viewer.json; appearance.theme and locale may be "inherit" (from the page)
     * @param {{ url: string, height?: "fill" | "auto", title?: string, urlParam?: string }} options where the Viewer is served, how its frame is
     *        sized, and the parameter of the address of the page that keeps the address of the Viewer
     */
    static create(config, options) {
        return new Viewer(config, options);
    }

    constructor(config, options) {
        if (!options || !options.url) {
            throw new ViewerError({ type: "config_error", code: "missing_option", param: "url", message: "The address of the Viewer (options.url, https://viewer.example.org/) is required." });
        }

        const url = new URL(options.url, document.baseURI);
        if (!url.pathname.endsWith("/")) {
            url.pathname += "/";
        }

        this.#url = url;
        this.#origin = url.origin;
        this.#config = clone(config ?? {});
        this.#height = options.height === "auto" ? "auto" : "fill";
        this.#title = options.title ?? "FHIR Viewer";
        this.#urlParam = options.urlParam ?? null;
    }

    /** The address of the Viewer (resources/Patient, summary/123); null before it is ready. */
    get location() {
        return this.#location;
    }

    /** The configuration in effect: the one given, with the updates that succeeded. */
    get options() {
        return clone(this.#config);
    }

    /** Puts the frame into an element (or the first element of a selector) and starts the Viewer. */
    mount(target) {
        const element = typeof target === "string" ? document.querySelector(target) : target;
        if (!element) {
            throw new ViewerError({ type: "config_error", code: "invalid_option", param: "target", message: `There is no element ${target} to mount the Viewer in.` });
        }

        if (this.#frame) {
            throw new ViewerError({ type: "state_error", code: "already_mounted", param: "target", message: "The Viewer is mounted already: destroy it first." });
        }

        const frame = document.createElement("iframe");
        frame.src = new URL("embed?host=parent", this.#url).href;
        frame.title = this.#title;
        frame.style.border = "0";
        frame.style.width = "100%";
        frame.style.display = "block";
        frame.style.height = this.#height === "fill" ? "100%" : "480px";
        this.#listener = event => this.#receive(event);
        window.addEventListener("message", this.#listener);
        element.appendChild(frame);
        this.#frame = frame;
        if (this.#urlParam) {
            this.#deepLink = new URL(window.location.href).searchParams.get(this.#urlParam);
        }

        // "inherit": the theme and the language of the page are watched and passed on, the frame gets plain values
        this.#inherited = inherited(this.#config);
        this.#observer = new MutationObserver(() => this.#followPage());
        this.#observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class", "data-theme", "data-bs-theme", "lang", "style"] });
        return this;
    }

    /** Subscribes to an event; returns the function that unsubscribes. */
    on(event, handler) {
        if (!EVENTS.includes(event)) {
            throw new ViewerError({ type: "config_error", code: "invalid_option", param: "event", message: `'${event}' is not an event of the Viewer: ${EVENTS.join(", ")}.` });
        }

        if (!this.#handlers.has(event)) {
            this.#handlers.set(event, new Set());
        }

        this.#handlers.get(event).add(handler);
        return () => this.off(event, handler);
    }

    off(event, handler) {
        this.#handlers.get(event)?.delete(handler);
    }

    /** Changes the options that need no restart (locale, appearance, terminology, clinicalSummary). Resolves with { applied }. */
    async update(changes) {
        const result = await this.#request("update", { config: resolve(changes) });
        this.#config = merge(this.#config, changes);
        this.#inherited = inherited(this.#config);
        return result;
    }

    /** Starts the Viewer anew with another configuration (another server, another SMART client). Resolves when it is ready. */
    async restart(config) {
        const result = await this.#request("restart", { config: resolve(config) });
        this.#config = clone(config);
        this.#inherited = inherited(this.#config);
        return result;
    }

    /**
     * Brings the Viewer to a configuration, the way a component of a framework passes its props: a restart when a key that needs one differs
     * (server, smart), else an update of the live keys that differ, else nothing. Resolves with the result of what it did, or null.
     */
    configure(config) {
        const current = this.#config;
        const keys = new Set([...Object.keys(current), ...Object.keys(config ?? {})]);
        if ([...keys].some(key => !LIVE.includes(key) && !same(current[key], config?.[key]))) {
            return this.restart(config);
        }

        const changes = {};
        for (const key of LIVE) {
            if (!same(current[key], config?.[key]) && config?.[key] !== undefined) {
                changes[key] = config[key];
            }
        }

        return Object.keys(changes).length > 0 ? this.update(changes) : Promise.resolve(null);
    }

    /** Goes to an address of the Viewer (Patient/123, summary/123, resources/Observation?code=8867-4). */
    navigate(location, { replace = false } = {}) {
        return this.#request("navigate", { location, replace });
    }

    /** Prints the page of the Viewer (the print dialog of the browser for the frame only). */
    print() {
        return this.#request("print", {});
    }

    /** Removes the frame; the promises that wait are rejected. */
    destroy() {
        for (const { reject } of this.#pending.values()) {
            reject(new ViewerError({ type: "state_error", code: "destroyed", param: "viewer", message: "The Viewer was destroyed." }));
        }

        this.#pending.clear();
        this.#queue = [];
        if (this.#listener) {
            window.removeEventListener("message", this.#listener);
        }

        this.#observer?.disconnect();
        this.#observer = null;
        this.#frame?.remove();
        this.#frame = null;
        this.#connected = false;
    }

    #request(type, payload) {
        if (!this.#frame) {
            return Promise.reject(new ViewerError({ type: "state_error", code: "not_mounted", param: "viewer", message: "Mount the Viewer first." }));
        }

        const id = String(++this.#sequence);
        const promise = new Promise((resolve, reject) => this.#pending.set(id, { resolve, reject }));
        if (this.#connected) {
            this.#post(type, payload, id);
        } else {
            this.#queue.push({ type, payload, id });
        }

        return promise;
    }

    #post(type, payload, id) {
        this.#frame?.contentWindow?.postMessage({ protocol: PROTOCOL, type, id, payload }, this.#origin);
    }

    #receive(event) {
        if (!this.#frame || event.source !== this.#frame.contentWindow || event.origin !== this.#origin) {
            return;
        }

        const message = event.data;
        if (!message || message.protocol !== PROTOCOL || typeof message.type !== "string") {
            return;
        }

        const payload = message.payload ?? {};
        switch (message.type) {
            case "hello":
                // The frame has loaded (also after the way back of a SMART redirect): it gets the configuration in effect, then the requests that
                // waited for it, in order
                this.#connected = true;
                this.#inherited = inherited(this.#config);
                this.#post("init", { config: resolve(this.#config) }, undefined);
                for (const queued of this.#queue.splice(0)) {
                    this.#post(queued.type, queued.payload, queued.id);
                }

                break;
            case "ready":
                this.#location = payload.location ?? null;
                this.#emit("ready", payload);
                // A link with the address of the Viewer in the address of the page opens there, once
                if (this.#deepLink && this.#deepLink !== this.#location) {
                    const link = this.#deepLink;
                    this.#deepLink = null;
                    this.navigate(link, { replace: true }).catch(error => this.#emit("error", error));
                } else {
                    this.#deepLink = null;
                    this.#writeUrl();
                }

                break;
            case "result": {
                const pending = this.#pending.get(message.id);
                if (pending) {
                    this.#pending.delete(message.id);
                    if (payload.ok) {
                        pending.resolve(payload.value ?? {});
                    } else {
                        pending.reject(new ViewerError(payload.error ?? { message: "The Viewer refused the request." }));
                    }
                }

                break;
            }
            case "navigate":
                this.#location = payload.location ?? null;
                this.#writeUrl();
                this.#emit("navigate", payload);
                break;
            case "resize":
                if (this.#height === "auto" && Number.isFinite(payload.height)) {
                    this.#frame.style.height = `${payload.height}px`;
                }

                break;
            default:
                if (EVENTS.includes(message.type)) {
                    this.#emit(message.type, payload);
                }
        }
    }

    // The address of the Viewer into the address of the page, without a new entry of the history: the moves inside the frame are in the
    // history of the tab already, a second entry would take two presses of Back
    #writeUrl() {
        if (!this.#urlParam || !this.#location) {
            return;
        }

        const url = new URL(window.location.href);
        if (url.searchParams.get(this.#urlParam) !== this.#location) {
            url.searchParams.set(this.#urlParam, this.#location);
            // Slashes stay readable (?fhir=summary/123): a slash needs no escaping in a query
            url.search = url.searchParams.toString().replace(/%2F/gi, "/");
            window.history.replaceState(window.history.state, "", url);
        }
    }

    // The page changed its theme or language: the values the frame has under "inherit" follow
    #followPage() {
        if (!this.#connected || !this.#inherited) {
            return;
        }

        const now = inherited(this.#config);
        const changes = {};
        if (now.theme !== this.#inherited.theme) {
            changes.appearance = { theme: now.theme };
        }

        if (now.locale !== this.#inherited.locale) {
            changes.locale = now.locale;
        }

        this.#inherited = now;
        if (Object.keys(changes).length > 0) {
            this.#request("update", { config: changes }).catch(error => this.#emit("error", error));
        }
    }

    #emit(event, payload) {
        for (const handler of this.#handlers.get(event) ?? []) {
            try {
                handler(payload);
            } catch (error) {
                console.error(error);
            }
        }
    }
}

// The theme of the page: data-theme or data-bs-theme (Bootstrap), the class dark or light, the CSS color-scheme; else the operating system
function pageTheme() {
    const root = document.documentElement;
    const data = `${root.dataset.theme ?? ""} ${root.dataset.bsTheme ?? ""}`.toLowerCase();
    if (data.includes("dark") || root.classList.contains("dark")) {
        return "dark";
    }

    if (data.includes("light") || root.classList.contains("light")) {
        return "light";
    }

    const scheme = getComputedStyle(root).colorScheme;
    return scheme === "dark" || scheme === "light" ? scheme : "auto";
}

// The language of the page (lang of <html>) when the Viewer has texts for it, else the language of the browser
function pageLocale() {
    const lang = (document.documentElement.lang ?? "").trim();
    const primary = lang.split("-")[0].toLowerCase();
    if (primary === "zh") {
        return /hant|tw|hk|mo/i.test(lang) ? "auto" : "zh-Hans";
    }

    return LOCALES.includes(primary) ? lang : "auto";
}

// The values "inherit" stands for now; null when nothing is inherited
function inherited(config) {
    const theme = config?.appearance?.theme === INHERIT ? pageTheme() : null;
    const locale = config?.locale === INHERIT ? pageLocale() : null;
    return theme || locale ? { theme, locale } : null;
}

// A configuration for the frame: "inherit" replaced by the values of the page
function resolve(config) {
    const result = clone(config ?? {});
    if (result.appearance?.theme === INHERIT) {
        result.appearance.theme = pageTheme();
    }

    if (result.locale === INHERIT) {
        result.locale = pageLocale();
    }

    return result;
}

// Equal as JSON, whatever the order of the keys
function same(a, b) {
    return JSON.stringify(canonical(a)) === JSON.stringify(canonical(b));
}

function canonical(value) {
    if (Array.isArray(value)) {
        return value.map(canonical);
    }

    if (value && typeof value === "object") {
        return Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])]));
    }

    return value;
}

function clone(value) {
    return JSON.parse(JSON.stringify(value));
}

// Objects merge by key, everything else is replaced (as the frame does)
function merge(target, changes) {
    const result = clone(target);
    for (const [key, value] of Object.entries(changes ?? {})) {
        result[key] = value && typeof value === "object" && !Array.isArray(value) && result[key] && typeof result[key] === "object"
            ? merge(result[key], value)
            : clone(value);
    }

    return result;
}
