// Pan and zoom of the drawing of a Bundle: a drag moves it, the wheel and a pinch zoom it at the pointer, all in the browser (no call to .NET per move).
// A drag is not a click: the click that ends it does not reach the node under it. A node that takes the focus from the keyboard is brought into view
const MinScale = 0.2;
const MaxScale = 2.5;
const DragThreshold = 4;

export function attach(viewport, canvas) {
    let scale = 1;
    let x = 0;
    let y = 0;
    let drag = null;
    let pinch = null;
    let moved = false;
    const pointers = new Map();

    function apply() {
        canvas.style.transform = `translate(${x}px, ${y}px) scale(${scale})`;
    }

    function zoomAt(px, py, factor) {
        const next = Math.min(MaxScale, Math.max(MinScale, scale * factor));
        x = px - (px - x) * (next / scale);
        y = py - (py - y) * (next / scale);
        scale = next;
        apply();
    }

    function local(e) {
        const box = viewport.getBoundingClientRect();
        return { x: e.clientX - box.left, y: e.clientY - box.top };
    }

    function onWheel(e) {
        e.preventDefault();
        const at = local(e);
        zoomAt(at.x, at.y, Math.exp(-e.deltaY * 0.0015));
    }

    function onDown(e) {
        if (e.pointerType === "mouse" && e.button !== 0) {
            return;
        }

        pointers.set(e.pointerId, local(e));
        moved = false;
        if (pointers.size === 1) {
            const at = pointers.get(e.pointerId);
            drag = { x: at.x, y: at.y, ox: x, oy: y };
        } else if (pointers.size === 2) {
            const [a, b] = [...pointers.values()];
            pinch = { distance: Math.hypot(a.x - b.x, a.y - b.y), scale };
            drag = null;
        }
    }

    function onMove(e) {
        if (!pointers.has(e.pointerId)) {
            return;
        }

        pointers.set(e.pointerId, local(e));
        if (pinch && pointers.size === 2) {
            const [a, b] = [...pointers.values()];
            const distance = Math.hypot(a.x - b.x, a.y - b.y);
            if (pinch.distance > 0) {
                moved = true;
                zoomAt((a.x + b.x) / 2, (a.y + b.y) / 2, (pinch.scale * distance / pinch.distance) / scale);
            }
        } else if (drag) {
            const at = pointers.get(e.pointerId);
            const dx = at.x - drag.x;
            const dy = at.y - drag.y;
            if (!moved && Math.hypot(dx, dy) > DragThreshold) {
                moved = true;
                viewport.setPointerCapture(e.pointerId);
            }

            if (moved) {
                x = drag.ox + dx;
                y = drag.oy + dy;
                apply();
            }
        }
    }

    function onUp(e) {
        pointers.delete(e.pointerId);
        if (pointers.size < 2) {
            pinch = null;
        }

        if (pointers.size === 0) {
            drag = null;
        }
    }

    function onClick(e) {
        if (moved) {
            e.stopPropagation();
            e.preventDefault();
            moved = false;
        }
    }

    function onFocus(e) {
        if (!(e.target instanceof HTMLElement) || !canvas.contains(e.target)) {
            return;
        }

        const box = viewport.getBoundingClientRect();
        const node = e.target.getBoundingClientRect();
        const margin = 16;
        let dx = 0;
        let dy = 0;
        if (node.left < box.left + margin) dx = box.left + margin - node.left;
        else if (node.right > box.right - margin) dx = box.right - margin - node.right;
        if (node.top < box.top + margin) dy = box.top + margin - node.top;
        else if (node.bottom > box.bottom - margin) dy = box.bottom - margin - node.bottom;
        if (dx !== 0 || dy !== 0) {
            x += dx;
            y += dy;
            apply();
        }
    }

    viewport.addEventListener("wheel", onWheel, { passive: false });
    viewport.addEventListener("pointerdown", onDown);
    viewport.addEventListener("pointermove", onMove);
    viewport.addEventListener("pointerup", onUp);
    viewport.addEventListener("pointercancel", onUp);
    viewport.addEventListener("click", onClick, true);
    viewport.addEventListener("focusin", onFocus);
    apply();

    return {
        // The whole drawing in view, not larger than its own size
        fit(width, height) {
            const box = viewport.getBoundingClientRect();
            if (width <= 0 || height <= 0 || box.width <= 0 || box.height <= 0) {
                return;
            }

            scale = Math.min(1, box.width / width, box.height / height);
            scale = Math.max(MinScale, scale);
            x = (box.width - width * scale) / 2;
            y = Math.max(0, (box.height - height * scale) / 2);
            apply();
        },
        zoom(factor) {
            const box = viewport.getBoundingClientRect();
            zoomAt(box.width / 2, box.height / 2, factor);
        },
        dispose() {
            viewport.removeEventListener("wheel", onWheel);
            viewport.removeEventListener("pointerdown", onDown);
            viewport.removeEventListener("pointermove", onMove);
            viewport.removeEventListener("pointerup", onUp);
            viewport.removeEventListener("pointercancel", onUp);
            viewport.removeEventListener("click", onClick, true);
            viewport.removeEventListener("focusin", onFocus);
        }
    };
}
