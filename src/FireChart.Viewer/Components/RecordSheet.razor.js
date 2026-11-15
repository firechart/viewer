// The sheet over a page: the page behind is inert while the sheet is open (Tab and a reader stay in the sheet), the sheet takes the focus, and the focus
// goes back to what had it (the row that opened the sheet) when the sheet goes
export function attach(panel) {
    const opener = document.activeElement;
    let page = null;
    panel.focus({ preventScroll: true });
    return {
        behind(element) {
            if (element && element !== page) {
                page = element;
                page.inert = true;
            }
        },
        focus() {
            panel.focus({ preventScroll: true });
        },
        dispose() {
            if (page) {
                page.inert = false;
            }

            if (opener instanceof HTMLElement && opener !== document.body && opener.isConnected && !opener.closest("[inert]")) {
                opener.focus({ preventScroll: true });
            }
        }
    };
}
