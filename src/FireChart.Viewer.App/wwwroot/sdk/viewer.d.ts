// Types of the FireChart Viewer SDK. Written by hand for now; generated from the C# records of the configuration later (plan 4.2).

/** The configuration of the Viewer: the keys of viewer.json. */
export interface ViewerConfig {
    /** The FHIR server; the only required key. */
    server: {
        /** The base address (https://fhir.example.org/r4). */
        url: string;
        /** The name shown in the title; default: the host of the address. */
        name?: string;
        /** STU3, R4, R4B, R5 or R6; default: from the CapabilityStatement. */
        fhirRelease?: "STU3" | "R4" | "R4B" | "R5" | "R6";
        /** The size of a page of a list; default 50. */
        pageSize?: number;
        /** How long one request to the server may take, in seconds (5 to 600); default 60. */
        timeoutSeconds?: number;
    };
    /** The SMART client, when the server needs a sign-in (a public client with PKCE: no secret). */
    smart?: {
        clientId: string;
        /** Default: openid fhirUser launch/patient patient/*.rs. */
        scopes?: string;
        /** redirect (default: the frame leaves and comes back) or popup (a window of its own). */
        flow?: "redirect" | "popup";
        /** The authority of the sign-in; default: the SMART discovery of the server. */
        authority?: string;
        /** More parameters of the authorization request, as a query (launch=e30). */
        extraParameters?: string;
    };
    /** auto (the language of the browser), inherit (lang of the page, followed live; SDK only) or en, de, es, he, ru, zh-Hans, with a region (ru-RU). */
    locale?: string;
    /** The FHIR settings; a change starts a new session of the Viewer (no reload of the page). */
    fhir?: {
        /** The terminology server for the displays of bare codes; none: the codes are shown as they are. */
        terminologyServer?: string;
        /** The page size (_count) of the searches of a summary whose form gives none: 1 to 500; default 20. */
        pageSize?: number;
        /** Load every record before the summary is shown, behind a panel of progress with Cancel; default false. */
        loadAll?: boolean;
        /** The most decimal places of a value: 0 to 10; default 2. More are rounded, without the zeros the rounding leaves (100.004 is 100). */
        decimals?: number;
    };
    appearance?: {
        /** inherit (SDK only): the theme of the page (data-theme, data-bs-theme, the class dark, color-scheme), followed live. */
        theme?: "light" | "dark" | "auto" | "inherit";
        /** A theme of the library, styles/themes/{style}.css: "fluent", "compact", or one of the appearance folder of the installation. */
        style?: string;
        /** Fonts to load (@font-face) for the style or fontFamily: https only. */
        fonts?: { family: string; src: string; weight?: string; style?: "normal" | "italic" }[];
        /** The public variables over the style (colorPrimary, fontFamily, borderRadius, ...). */
        variables?: Record<string, string>;
        /** Properties of public selectors (.Banner, .Tile, .Badge--status, ...). */
        rules?: Record<string, Record<string, string>>;
    };
}

/** The options that change without a restart. */
export type ViewerUpdate = Pick<ViewerConfig, "locale" | "fhir" | "appearance">;

export interface ViewerOptions {
    /** Where FireChart.Viewer.App is served (https://viewer.example.org/); its host.json must allow the origin of this page. */
    url: string;
    /** fill (default): the frame fills the element, give the element a height; auto: the frame takes the height of its content. */
    height?: "fill" | "auto";
    /** The title of the frame, for screen readers; default "FHIR Viewer". */
    title?: string;
    /** A parameter of the address of the page that keeps the address of the Viewer (?fhir=summary/Patient/123): a link or a reload opens the Viewer there. */
    urlParam?: string;
}

export interface ViewerErrorData {
    /** config_error, navigation_error, protocol_error, state_error; bundle_error of the Bundle Visualizer. */
    type: string;
    /** unknown_option, missing_option, invalid_option, invalid_value, restart_required, invalid_location, not_ready, origin_not_allowed (the origin of the page is
     * not in host.json of the Viewer), internal_error, not_mounted, already_mounted, destroyed; of a Bundle
     * invalid_bundle, unavailable, superseded, ... */
    code: string;
    /** The path of the option (appearance.theme) or the argument. */
    param: string;
    message: string;
}

export declare class ViewerError extends Error implements ViewerErrorData {
    type: string;
    code: string;
    param: string;
}

export interface ViewerEvents {
    /** The Viewer runs with a configuration (after mount, a restart, the way back of a SMART redirect). */
    ready: { location: string | null; server: string };
    /** The address of the Viewer changed: by the user, or by navigate() ("host"). */
    navigate: { location: string; source: "user" | "host" };
    /** A restart began. */
    restarting: Record<string, never>;
    /** An update was applied. */
    updated: { applied: string[] };
    /** An option or a message the Viewer could not use; the Viewer goes on without it. */
    error: ViewerErrorData;
}

export declare class Viewer {
    static create(config: ViewerConfig, options: ViewerOptions): Viewer;
    /** The address of the Viewer; null before it is ready. */
    readonly location: string | null;
    /** The configuration in effect. */
    readonly options: ViewerConfig;
    mount(target: string | Element): this;
    on<K extends keyof ViewerEvents>(event: K, handler: (payload: ViewerEvents[K]) => void): () => void;
    off<K extends keyof ViewerEvents>(event: K, handler: (payload: ViewerEvents[K]) => void): void;
    update(changes: ViewerUpdate): Promise<{ applied: string[] }>;
    restart(config: ViewerConfig): Promise<{ location: string | null; server: string }>;
    /** A restart when server or smart differ, else an update of the live keys that differ, else nothing (null): for the props of a component. */
    configure(config: ViewerConfig): Promise<{ applied: string[] } | { location: string | null; server: string } | null>;
    navigate(location: string, options?: { replace?: boolean }): Promise<{ location: string }>;
    /** Prints the page of the Viewer: a summary loads all its records first and is not printed when some cannot be loaded; another page opens the
     * print dialog of the browser as it is (a list prints the rows in view, with a note that the printout is incomplete). */
    print(): Promise<Record<string, never>>;
    destroy(): void;
}

/** The configuration of the Bundle Visualizer: the keys of viewer.json, the server optional. */
export interface BundleVisualizerConfig {
    /** The FHIR server of a Bundle read by an address (its release is the release of the Bundle); a pasted Bundle needs none. */
    server?: ViewerConfig["server"];
    /** The SMART client of the server, when it needs a sign-in: the sign-in comes before the address is read. After a redirect the SDK shows the last
     * Bundle again by itself; "popup" keeps the frame. */
    smart?: ViewerConfig["smart"];
    locale?: ViewerConfig["locale"];
    /** The release assumed for a Bundle without a server (fhirRelease of the server otherwise) and the decimal places of a value on a card. */
    fhir?: ViewerConfig["fhir"];
    appearance?: ViewerConfig["appearance"];
}

/** What show() draws: a Bundle (the parsed JSON, or a text of JSON or XML), or the address to read it from (absolute, or relative to server). */
export type BundleSource = object | string | { location: string };

/** The counts of a Bundle drawn. */
export interface BundleShown {
    /** The entries that hold a resource. */
    resources: number;
    /** The references drawn, to the resources the Bundle lacks too. */
    references: number;
}

export interface BundleVisualizerEvents {
    /** The frame runs with a configuration (after mount, a restart). */
    ready: { server: string | null };
    /** A Bundle is drawn (after show, or again after a change of the language). */
    shown: BundleShown;
    /** A resource was chosen (its sheet opened), or the sheet closed (null). */
    select: { reference: string | null };
    restarting: Record<string, never>;
    updated: { applied: string[] };
    /** An option, a message or a Bundle the frame could not use. */
    error: ViewerErrorData;
}

export declare class BundleVisualizer {
    static create(config: BundleVisualizerConfig, options: Omit<ViewerOptions, "urlParam">): BundleVisualizer;
    /** The reference of the resource whose sheet is open; null when none is. */
    readonly selected: string | null;
    /** The configuration in effect. */
    readonly options: BundleVisualizerConfig;
    mount(target: string | Element): this;
    on<K extends keyof BundleVisualizerEvents>(event: K, handler: (payload: BundleVisualizerEvents[K]) => void): () => void;
    off<K extends keyof BundleVisualizerEvents>(event: K, handler: (payload: BundleVisualizerEvents[K]) => void): void;
    /** Draws a Bundle; resolves when it is drawn (after the sign-in, when the server needs one), rejects with a bundle_error (invalid_bundle,
     * unavailable, cancelled by the user, superseded) or a config_error. The same Bundle again after a failure or a cancel is read anew. */
    show(bundle: BundleSource): Promise<BundleShown>;
    update(changes: ViewerUpdate): Promise<{ applied: string[] }>;
    /** Starts anew with another configuration; nothing is shown after it. */
    restart(config: BundleVisualizerConfig): Promise<{ server: string | null }>;
    configure(config: BundleVisualizerConfig): Promise<{ applied: string[] } | { server: string | null } | null>;
    destroy(): void;
}
