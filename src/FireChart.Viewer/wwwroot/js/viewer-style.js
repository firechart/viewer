// The sheet of the kit fx-* in the head of the document, put there once and kept: WebKit registers the font faces again whenever a sheet comes or goes,
// and text laid out meanwhile keeps the invisible fallback of the icon font (iOS: no icons on the first page of a list). A <style> of each page came
// and went with every page, in the same render as its rows
export function adopt(css) {
    let style = document.getElementById('fx-kit-style');
    if (!style) {
        style = document.createElement('style');
        style.id = 'fx-kit-style';
        document.head.appendChild(style);
    }

    if (style.textContent !== css) {
        style.textContent = css;
    }
}
