import { createEditor } from "../lib/codemirror/codemirror.min.js";

// elementId -> { editor, observer }
const instances = new Map();

const isDark = () => document.documentElement.getAttribute("data-theme") === "dark";

export function init(elementId, language, value, readOnly, wrap, autoHeight) {
    destroy(elementId);

    const parent = document.getElementById(elementId);
    if (!parent) {
        return;
    }

    const editor = createEditor(parent, { language, value, readOnly, wrap, autoHeight, dark: isDark() });

    // FloxThemeHost switches the theme through data-theme on <html>
    const observer = new MutationObserver(() => editor.setDark(isDark()));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

    instances.set(elementId, { editor, observer });
}

const withEditor = (elementId, action) => {
    const instance = instances.get(elementId);
    return instance ? action(instance.editor) : undefined;
};

export const getValue = (elementId) => withEditor(elementId, (e) => e.getValue()) ?? "";
export const setValue = (elementId, value) => withEditor(elementId, (e) => e.setValue(value));
export const setLanguage = (elementId, language) => withEditor(elementId, (e) => e.setLanguage(language));
export const setReadOnly = (elementId, readOnly) => withEditor(elementId, (e) => e.setReadOnly(readOnly));
export const setWrap = (elementId, wrap) => withEditor(elementId, (e) => e.setWrap(wrap));
export const setAutoHeight = (elementId, autoHeight) => withEditor(elementId, (e) => e.setAutoHeight(autoHeight));

export function destroy(elementId) {
    const instance = instances.get(elementId);
    if (instance) {
        instance.observer.disconnect();
        instance.editor.destroy();
        instances.delete(elementId);
    }
}
