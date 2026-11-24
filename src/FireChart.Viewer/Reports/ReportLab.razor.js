// The width of the rows of a section of laboratory results: narrow below 36rem, as the container query fx-rows of fx.css says. The component draws only
// the form the width shows (the table, or the tiles of the results); told again only when the width crosses the line
export function observe(element, dotnet) {
    const container = element?.closest(".fx-summary-rows") ?? element?.parentElement;
    if (!container) {
        return null;
    }

    let narrow = null;
    const observer = new ResizeObserver(entries => {
        const width = entries[entries.length - 1].contentRect.width;
        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
        const now = width < 36 * rem;
        if (now !== narrow) {
            narrow = now;
            dotnet.invokeMethodAsync("OnWidth", now);
        }
    });
    observer.observe(container);

    return {
        dispose() {
            observer.disconnect();
        }
    };
}
