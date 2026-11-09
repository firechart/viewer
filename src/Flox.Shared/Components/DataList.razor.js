// Touch gestures of a DataList: a long press on a row opens its menu, a pull down at the top refreshes the list.
// A mouse keeps the context menu of the browser (a right click); touch is handled here, as iOS sends no contextmenu event

const LONG_PRESS_MS = 500;
const MOVE_TOLERANCE_PX = 10;
const PULL_THRESHOLD_PX = 72;
const PULL_MAX_PX = 120;

export function attach(root, scroller, dotnet, options) {
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

    let pullStartY = null;
    let pull = 0;
    const indicator = options.pull ? scroller.querySelector("[data-pull]") : null;

    const showPull = (distance, animate) => {
        if (!indicator) {
            return;
        }

        indicator.style.transition = animate ? "height 150ms ease" : "none";
        indicator.style.height = `${distance}px`;
        indicator.style.opacity = String(Math.min(1, distance / PULL_THRESHOLD_PX));
        const icon = indicator.firstElementChild;
        if (icon) {
            icon.style.transform = `rotate(${(distance / PULL_THRESHOLD_PX) * 270}deg)`;
        }
    };

    const onTouchStart = (e) => {
        pullStartY = scroller.scrollTop <= 0 && e.touches.length === 1 ? e.touches[0].clientY : null;
        pull = 0;
    };

    const onTouchMove = (e) => {
        if (pullStartY === null) {
            return;
        }

        const dy = e.touches[0].clientY - pullStartY;
        if (dy <= 0 || scroller.scrollTop > 0) {
            if (pull > 0) {
                pull = 0;
                showPull(0, true);
            }

            return;
        }

        pull = Math.min(dy * 0.5, PULL_MAX_PX);
        showPull(pull, false);
        if (e.cancelable) {
            e.preventDefault();
        }
    };

    const onTouchEnd = () => {
        const refresh = pull >= PULL_THRESHOLD_PX;
        pullStartY = null;
        pull = 0;
        showPull(0, true);
        if (refresh) {
            dotnet.invokeMethodAsync("OnPullRefresh");
        }
    };

    if (indicator) {
        scroller.addEventListener("touchstart", onTouchStart, { passive: true });
        scroller.addEventListener("touchmove", onTouchMove, { passive: false });
        scroller.addEventListener("touchend", onTouchEnd, { passive: true });
        scroller.addEventListener("touchcancel", onTouchEnd, { passive: true });
    }

    return {
        dispose() {
            cancelPress();
            root.removeEventListener("pointerdown", onPointerDown);
            root.removeEventListener("pointermove", onPointerMove);
            root.removeEventListener("pointerup", cancelPress);
            root.removeEventListener("pointercancel", cancelPress);
            root.removeEventListener("click", onClick, true);
            root.removeEventListener("contextmenu", onContextMenu, true);
            if (indicator) {
                scroller.removeEventListener("touchstart", onTouchStart);
                scroller.removeEventListener("touchmove", onTouchMove);
                scroller.removeEventListener("touchend", onTouchEnd);
                scroller.removeEventListener("touchcancel", onTouchEnd);
            }
        }
    };
}
