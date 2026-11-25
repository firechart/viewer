// The place of the page in the history of the tab: Blazor numbers each entry it pushes (history.state._index, from 0 at the first page; a replace
// and a reload keep it, the browser's Back and Forward read it), so an index above 0 is a page of the app to go back to
export function installHistory() {
    window.floxHistory = {
        index: () => history.state?._index ?? 0,
        back: () => history.back()
    };
}
