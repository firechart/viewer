// The list of the clinical summary: the rows have one height, so the window of the rows to draw follows from the scroll position alone.
// The height is the CSS of the kit and changes with the width of the list (the probe is as tall as a row): the first row in view is kept when it changes.
// The only scrolling element is the scroller; it is made as tall as the free part of the screen, so nothing around it scrolls (nested scrolling is bad on phones)

export function attach(scroller, probe, dotnet, bottomGap, initialTop) {
    // The list left the page before it was attached
    if (!scroller || !probe) {
        return null;
    }

    let frame = 0;
    let lastFirst = -1;
    let lastVisible = -1;
    let saveTimer = 0;
    let savedTop = initialTop || 0;

    const rowPx = () => probe.getBoundingClientRect().height || 56;
    let rowHeight = rowPx();

    // The rows got another height (the list crossed the width of the narrow layout): the same row stays at the top
    const onRowHeight = () => {
        const height = rowPx();
        if (height === rowHeight) {
            return;
        }

        const first = scroller.scrollTop / rowHeight;
        rowHeight = height;
        scroller.scrollTop = first * height;
        lastFirst = -1;
        schedule();
    };

    const fit = () => {
        const available = window.visualViewport ? window.visualViewport.height : window.innerHeight;
        const height = Math.max(160, available - scroller.getBoundingClientRect().top - bottomGap);
        scroller.style.height = height + "px";

        // What still sticks out of the page (its own padding, the border of the list) comes off the list: the page itself must not scroll
        const page = scroller.parentElement.closest(".rz-body") ?? document.scrollingElement;
        const over = page.scrollHeight - page.clientHeight;
        if (over > 0) {
            scroller.style.height = Math.max(160, height - over) + "px";
        }
    };

    // The floating title is as wide as the rows, not as the list with its scrollbar: the width of the scrollbar goes to the style of the list as a variable
    const syncBar = () => {
        scroller.parentElement.style.setProperty("--fx-scrollbar", (scroller.offsetWidth - scroller.clientWidth) + "px");
    };

    const report = () => {
        frame = 0;
        const height = rowPx();
        // the rubber band of iOS gives a negative scrollTop over the top of the list
        const first = Math.max(0, Math.floor(scroller.scrollTop / height));
        const visible = Math.ceil(scroller.clientHeight / height) + 1;
        if (first !== lastFirst || visible !== lastVisible) {
            lastFirst = first;
            lastVisible = visible;
            dotnet.invokeMethodAsync("OnScroll", first, visible);
        }
    };

    // A frame is not drawn in a hidden or throttled tab: a timer answers then
    const schedule = () => {
        if (!frame) {
            frame = setTimeout(report, 50);
        }

        // the position is kept by the state of the page, a little after the scrolling stops
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => {
            const top = Math.max(0, scroller.scrollTop);
            if (top !== savedTop) {
                savedTop = top;
                dotnet.invokeMethodAsync("OnScrollTop", savedTop);
            }
        }, 200);
    };

    const onResize = () => {
        fit();
        schedule();
    };

    const barObserver = new ResizeObserver(syncBar);
    barObserver.observe(scroller);
    barObserver.observe(scroller.firstElementChild);
    const rowObserver = new ResizeObserver(onRowHeight);
    rowObserver.observe(probe);
    scroller.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", onResize);
    fit();
    syncBar();
    if (initialTop > 0) {
        scroller.scrollTop = initialTop;
    }

    report();

    return {
        scrollTo(index) {
            scroller.scrollTop = index * rowPx();
            schedule();
        },
        refit() {
            fit();
            schedule();
        },
        dispose() {
            clearTimeout(frame);
            clearTimeout(saveTimer);
            barObserver.disconnect();
            rowObserver.disconnect();
            scroller.removeEventListener("scroll", schedule);
            window.removeEventListener("resize", onResize);
        }
    };
}
