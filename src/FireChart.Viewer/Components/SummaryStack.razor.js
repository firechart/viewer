// The scroller of the clinical summary: as tall as what is left of the window (the page itself does not scroll), its place kept for the state of the page,
// and a section brought to its top by id
export function attach(scroller, dotnet, bottomGap, initialTop) {
    if (!scroller) {
        return null;
    }

    let saveTimer = 0;
    let savedTop = initialTop || 0;

    const fit = () => {
        const available = window.visualViewport ? window.visualViewport.height : window.innerHeight;
        const height = Math.max(160, available - scroller.getBoundingClientRect().top - bottomGap);
        scroller.style.height = height + "px";

        const page = scroller.parentElement.closest(".rz-body") ?? document.scrollingElement;
        const over = page.scrollHeight - page.clientHeight;
        if (over > 0) {
            scroller.style.height = Math.max(160, height - over) + "px";
        }
    };

    const onScroll = () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => {
            const top = Math.max(0, scroller.scrollTop);
            if (top !== savedTop) {
                savedTop = top;
                dotnet.invokeMethodAsync("OnScrollTop", savedTop);
            }
        }, 200);
    };

    scroller.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", fit);
    fit();
    if (initialTop > 0) {
        // the sections draw their rows after the first render: the place is restored when there is enough content for it
        const restore = () => {
            if (scroller.scrollHeight - scroller.clientHeight >= initialTop) {
                scroller.scrollTop = initialTop;
                observer.disconnect();
            }
        };
        const observer = new ResizeObserver(restore);
        observer.observe(scroller.firstElementChild ?? scroller);
        restore();
        setTimeout(() => observer.disconnect(), 5000);
    }

    return {
        scrollTo(id) {
            const target = scroller.querySelector("#" + CSS.escape(id));
            if (target) {
                scroller.scrollTop += target.getBoundingClientRect().top - scroller.getBoundingClientRect().top;
            }
        },
        refit() {
            fit();
        },
        dispose() {
            clearTimeout(saveTimer);
            scroller.removeEventListener("scroll", onScroll);
            window.removeEventListener("resize", fit);
        }
    };
}
