// The text of a file of the application (relative to the base of the page), or null when it has none
export async function loadText(url) {
    const response = await fetch(url, { cache: 'no-cache' });
    return response.ok ? await response.text() : null;
}
