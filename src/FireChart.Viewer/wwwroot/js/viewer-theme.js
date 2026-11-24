// The theme of the Viewer on <html data-theme> and the class dark of Blazor Blueprint, for a Viewer that owns its document (the frame); "auto" follows
// the operating system
let media = null;
let listener = null;

// data-theme drives the styles of the Viewer, the class dark the tokens and the dark variant of Blazor Blueprint
function apply(resolved) {
    document.documentElement.setAttribute("data-theme", resolved);
    document.documentElement.classList.toggle("dark", resolved === "dark");
}

function resolve(theme) {
    return theme === "auto" ? (media.matches ? "dark" : "light") : theme;
}

export function setTheme(theme) {
    media ??= window.matchMedia("(prefers-color-scheme: dark)");
    if (listener) {
        media.removeEventListener("change", listener);
        listener = null;
    }

    if (theme === "auto") {
        listener = () => {
            apply(resolve("auto"));
        };
        media.addEventListener("change", listener);
    }

    const resolved = resolve(theme);
    apply(resolved);
    return resolved === "dark";
}
