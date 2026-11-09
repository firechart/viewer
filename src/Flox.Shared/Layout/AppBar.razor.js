// Radzen collapses its responsive sidebar at this width
const NARROW_QUERY = "(max-width: 768px)";

export function isNarrow() {
    return window.matchMedia(NARROW_QUERY).matches;
}
