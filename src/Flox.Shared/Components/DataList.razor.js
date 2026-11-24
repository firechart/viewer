// Touch gestures of a DataList: a long press on a row opens its menu (a pull down is the page's, PullRefreshHost).
// A mouse keeps the context menu of the browser (a right click); touch is handled here, as iOS sends no contextmenu event

const LONG_PRESS_MS = 500;
const MOVE_TOLERANCE_PX = 10;

export function attach(root, dotnet) {
    let pointerType = "mouse";
    let pressTimer = 0;
    let pressStart = null;
    let suppressClick = false;

    const cancelPress = () => {
        clearTimeout(pressTimer);
        pressTimer = 0;
        pressStart = null;
    };

    const onPointerDown = (e) => {
        suppressClick = false;
        pointerType = e.pointerType;
        if (e.pointerType === "mouse") {
            return;
        }

        const row = e.target.closest("[data-row-id]");
        if (!row) {
            return;
        }

        pressStart = { x: e.clientX, y: e.clientY };
        pressTimer = setTimeout(() => {
            const index = Number(row.dataset.rowId);
            const { x, y } = pressStart;
            cancelPress();
            suppressClick = true;
            dotnet.invokeMethodAsync("OnLongPress", index, x, y);
        }, LONG_PRESS_MS);
    };

    const onPointerMove = (e) => {
        if (pressStart && Math.hypot(e.clientX - pressStart.x, e.clientY - pressStart.y) > MOVE_TOLERANCE_PX) {
            cancelPress();
        }
    };

    // The click that ends a long press must not open or select the row
    const onClick = (e) => {
        if (suppressClick) {
            suppressClick = false;
            e.preventDefault();
            e.stopPropagation();
        }
    };

    // Android sends contextmenu after a long press: the press is already handled
    const onContextMenu = (e) => {
        if (pointerType !== "mouse") {
            e.preventDefault();
            e.stopPropagation();
        }
    };

    root.addEventListener("pointerdown", onPointerDown);
    root.addEventListener("pointermove", onPointerMove);
    root.addEventListener("pointerup", cancelPress);
    root.addEventListener("pointercancel", cancelPress);
    root.addEventListener("click", onClick, true);
    root.addEventListener("contextmenu", onContextMenu, true);

    return {
        dispose() {
            cancelPress();
            root.removeEventListener("pointerdown", onPointerDown);
            root.removeEventListener("pointermove", onPointerMove);
            root.removeEventListener("pointerup", cancelPress);
            root.removeEventListener("pointercancel", cancelPress);
            root.removeEventListener("click", onClick, true);
            root.removeEventListener("contextmenu", onContextMenu, true);
        }
    };
}
