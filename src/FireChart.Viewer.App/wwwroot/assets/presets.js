// The public test servers of the demo pages: the presets of the playground (demo-presets.json, JSON with comment lines), the one of the page
// in its address (?server=).

/**
 * Fills the select of the servers and returns the presets with the one the address names (or the first).
 * @param {string} app the address of the application, where demo-presets.json is
 * @param {HTMLSelectElement} select the list of the servers
 * @returns {Promise<{ presets: object[], selected: object }>}
 */
export async function loadPresets(app, select) {
    const text = await (await fetch(new URL("demo-presets.json", app))).text();
    const presets = JSON.parse(text.replace(/^\s*\/\/.*$/gm, ""));
    for (const preset of presets) {
        select.add(new Option(preset.name, preset.id));
    }
    const selected = presets.find(p => p.id === new URLSearchParams(location.search).get("server")) ?? presets[0];
    select.value = selected.id;
    return { presets, selected };
}

/**
 * Puts the server into the address of the page.
 * @param {object} preset the preset chosen
 */
export function rememberServer(preset) {
    const url = new URL(location.href);
    url.searchParams.set("server", preset.id);
    history.replaceState(history.state, "", url);
}
