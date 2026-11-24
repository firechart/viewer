// A pull down at the top of the page on a touch screen: the page refreshes what it shows, the browser does not reload.
// One gesture for every page: it starts only where nothing between the finger and the page is scrolled down (a list of its own too)

const PULL_THRESHOLD_PX = 72;
const PULL_MAX_PX = 120;
const PULL_HOLD_PX = 48;

export function attach(indicator, dotnet, enabled) {
    const scroller = document.scrollingElement;
    const icon = indicator.firstElementChild;
    let startX = 0;
    let startY = null;
    let pull = 0;
    let busy = false;

    const show = (distance, animate) => {
        indicator.style.transition = animate ? "height 150ms ease" : "none";
        indicator.style.height = `${distance}px`;
        indicator.style.opacity = String(Math.min(1, distance / PULL_THRESHOLD_PX));
        if (icon && !busy) {
            icon.style.transform = `rotate(${(distance / PULL_THRESHOLD_PX) * 270}deg)`;
        }
    };

    const atTop = (target) => {
        for (let element = target; element; element = element.parentElement) {
            if (element.scrollTop > 0) {
                return false;
            }

            if (element === scroller) {
                return true;
            }
        }

        return scroller.scrollTop <= 0;
    };

    const onTouchStart = (e) => {
        pull = 0;
        startY = enabled && !busy && e.touches.length === 1 && atTop(e.target) ? e.touches[0].clientY : null;
        startX = e.touches[0]?.clientX ?? 0;
    };

    const onTouchMove = (e) => {
        if (startY === null) {
            return;
        }

        const dy = e.touches[0].clientY - startY;
        const dx = Math.abs(e.touches[0].clientX - startX);
        if (dy <= 0 || (pull === 0 && dx > dy)) {
            if (pull > 0) {
                pull = 0;
                show(0, true);
            }

            if (dx > Math.abs(dy)) {
                startY = null;
            }

            return;
        }

        pull = Math.min(dy * 0.5, PULL_MAX_PX);
        show(pull, false);
        if (e.cancelable) {
            e.preventDefault();
        }
    };

    const onTouchEnd = async () => {
        const refresh = pull >= PULL_THRESHOLD_PX;
        startY = null;
        pull = 0;
        if (!refresh) {
            show(0, true);
            return;
        }

        busy = true;
        icon?.classList.add("animate-spin");
        show(PULL_HOLD_PX, true);
        try {
            await dotnet.invokeMethodAsync("OnPull");
        } catch {
            // the page is gone or the handler failed; it shows its own error
        } finally {
            busy = false;
            icon?.classList.remove("animate-spin");
            show(0, true);
        }
    };

    scroller.addEventListener("touchstart", onTouchStart, { passive: true });
    scroller.addEventListener("touchmove", onTouchMove, { passive: false });
    scroller.addEventListener("touchend", onTouchEnd, { passive: true });
    scroller.addEventListener("touchcancel", onTouchEnd, { passive: true });

    return {
        enable(value) {
            enabled = value;
        },
        dispose() {
            scroller.removeEventListener("touchstart", onTouchStart);
            scroller.removeEventListener("touchmove", onTouchMove);
            scroller.removeEventListener("touchend", onTouchEnd);
            scroller.removeEventListener("touchcancel", onTouchEnd);
        }
    };
}
