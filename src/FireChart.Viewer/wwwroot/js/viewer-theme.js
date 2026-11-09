// The theme of the Viewer on <html data-theme>, for a Viewer that owns its document (the frame); "auto" follows the operating system and tells .NET,
// which switches the base theme of Radzen
let media = null;
let listener = null;

function resolve(theme) {
    return theme === "auto" ? (media.matches ? "dark" : "light") : theme;
}

export function setTheme(theme, reference) {
    media ??= window.matchMedia("(prefers-color-scheme: dark)");
    if (listener) {
        media.removeEventListener("change", listener);
        listener = null;
    }

    if (theme === "auto") {
        listener = () => {
            const resolved = resolve("auto");
            document.documentElement.setAttribute("data-theme", resolved);
            reference?.invokeMethodAsync("OnSystemThemeChanged", resolved === "dark");
        };
        media.addEventListener("change", listener);
    }

    const resolved = resolve(theme);
    document.documentElement.setAttribute("data-theme", resolved);
    return resolved === "dark";
}
