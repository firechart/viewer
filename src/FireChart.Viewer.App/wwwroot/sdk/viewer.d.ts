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
    /** auto (the language of the browser), inherit (lang of the page, followed live; SDK only) or en, de, es, ru, zh-Hans, with a region (ru-RU). */
    locale?: string;
    terminology?: {
        /** The terminology server for the displays of bare codes. */
        server?: string;
    };
    clinicalSummary?: {
        /** The code of the form of the clinical summary; default "default". */
        form?: string;
    };
    appearance?: {
        /** inherit (SDK only): the theme of the page (data-theme, data-bs-theme, the class dark, color-scheme), followed live. */
        theme?: "light" | "dark" | "auto" | "inherit";
        /** The public variables (colorPrimary, fontFamily, borderRadius, ...). */
        variables?: Record<string, string>;
        /** Properties of public selectors (.Banner, .Tile, .Badge--status, ...). */
        rules?: Record<string, Record<string, string>>;
    };
}

/** The options that change without a restart. */
export type ViewerUpdate = Pick<ViewerConfig, "locale" | "terminology" | "clinicalSummary" | "appearance">;

export interface ViewerOptions {
    /** Where FireChart.Viewer.App is served (https://viewer.example.org/); its host.json must allow the origin of this page. */
    url: string;
    /** fill (default): the frame fills the element, give the element a height; auto: the frame takes the height of its content. */
    height?: "fill" | "auto";
    /** The title of the frame, for screen readers; default "FHIR Viewer". */
    title?: string;
    /** A parameter of the address of the page that keeps the address of the Viewer (?fhir=summary/123): a link or a reload opens the Viewer there. */
    urlParam?: string;
}

export interface ViewerErrorData {
    /** config_error, navigation_error, protocol_error, state_error. */
    type: string;
    /** unknown_option, missing_option, invalid_option, invalid_value, restart_required, invalid_location, not_mounted, destroyed, ... */
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
    /** The print dialog of the browser for the page of the Viewer. */
    print(): Promise<Record<string, never>>;
    destroy(): void;
}
