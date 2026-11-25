// The appearance of the Viewer and of the Bundle Visualizer on a demo page: the dialog of the gear (language, theme, style, the names of the codes), kept in the address of
// the page (?lang=, ?theme=, ?style=, ?tx=) so a link opens the Viewer as it was. A default value is left out of the address.
const fields = {
    lang: { initial: "auto", change: value => ({ locale: value }), config: (config, value) => ({ ...config, locale: value }) },
    theme: {
        initial: "auto",
        change: value => ({ appearance: { theme: value } }),
        config: (config, value) => ({ ...config, appearance: { ...config.appearance, theme: value } })
    },
    style: {
        initial: "",
        change: value => ({ appearance: { style: value || null } }),
        config: (config, value) => value ? { ...config, appearance: { ...config.appearance, style: value } } : config
    },
    tx: {
        initial: "",
        change: value => ({ fhir: { terminologyServer: value || null } }),
        config: (config, value) => value ? { ...config, fhir: { ...config.fhir, terminologyServer: value } } : config
    }
};

// Each change is an update of the frame, without a restart
const markup = `
<dialog id="appearance" class="dialog" aria-labelledby="appearance-title">
<h2 id="appearance-title">Appearance</h2>
<label>
    Language
    <select name="lang">
        <option value="auto">Browser language</option>
        <option value="en">English</option>
        <option value="de">Deutsch</option>
        <option value="es">Español</option>
        <option value="he">עברית</option>
        <option value="ru">Русский</option>
        <option value="zh-Hans">中文（简体）</option>
    </select>
</label>
<label>
    Theme
    <select name="theme">
        <option value="auto">System</option>
        <option value="light">Light</option>
        <option value="dark">Dark</option>
    </select>
</label>
<label>
    Style
    <select name="style">
        <option value="">FireChart</option>
        <option value="fluent">Fluent</option>
        <option value="compact">Compact</option>
    </select>
</label>
<label>
    Names of codes
    <select name="tx">
        <option value="">As the record has them</option>
        <option value="https://tx.fhir.org/r4">From tx.fhir.org</option>
    </select>
</label>
<form method="dialog" class="actions">
    <button class="button primary">Done</button>
</form>
</dialog>`;

/**
 * Adds the dialog of the appearance to the page and binds it to a Viewer or a Bundle Visualizer.
 * @param {HTMLElement} opener the button that opens it
 * @returns {{ apply: (config: object) => object, attach: (viewer: { update: (changes: object) => Promise<unknown> }) => void }}
 */
export function bindAppearance(opener) {
    document.body.insertAdjacentHTML("beforeend", markup);
    const dialog = document.body.lastElementChild;
    const query = new URLSearchParams(location.search);
    const values = {};
    for (const [name, field] of Object.entries(fields)) {
        const select = dialog.querySelector(`[name="${name}"]`);
        if (!select) {
            continue;
        }
        const asked = query.get(name);
        values[name] = asked !== null && [...select.options].some(option => option.value === asked) ? asked : field.initial;
        select.value = values[name];
    }

    let viewer = null;
    opener.onclick = () => dialog.showModal();
    // A click on the backdrop closes the dialog too
    dialog.addEventListener("click", e => {
        if (e.target === dialog) {
            dialog.close();
        }
    });
    dialog.addEventListener("change", e => {
        const name = e.target.name;
        if (!(name in values)) {
            return;
        }
        values[name] = e.target.value;
        const url = new URL(location.href);
        if (values[name] === fields[name].initial) {
            url.searchParams.delete(name);
        } else {
            url.searchParams.set(name, values[name]);
        }
        history.replaceState(history.state, "", url);
        viewer?.update(fields[name].change(values[name])).catch(console.error);
    });

    return {
        // The configuration of a server with the appearance chosen here
        apply: config => Object.entries(values).reduce((result, [name, value]) => fields[name].config(result, value), config),
        attach: target => viewer = target
    };
}
