const drawers = new Map();

function clickHandler(e) {
    for (const [id, state] of drawers.entries()) {
        if (state.isOpen && !state.isPinned) {
            const el = document.getElementById(id);
            // Ignore if clicked inside the drawer
            if (el && el.contains(e.target)) {
                continue;
            }
            // Ignore if clicked on the button that opens it (heuristic: if button has jsini-icon-robot or similar, but safer is just to let the toggle button handle itself if it stops propagation. Wait, if toggle button is clicked, it might close and then toggle opens it? Actually, clicking the toggle button will be an outside click, so it closes the drawer, and then the toggle button's onClick fires and toggles it back open. To prevent this, usually toggle buttons stop propagation or we can check if it's a known button).
            // It's better if caller uses stopPropagation on toggle buttons, or we delay the close slightly.
            
            // To prevent toggle button double action, we can check if click is on a header/menu button.
            // A simple check: if the click is on an element with a class starting with 'jsini-icon-', maybe skip?
            // Actually, we'll just invoke the .NET method and let .NET handle it.
            state.dotnet.invokeMethodAsync('CloseFromOutside').catch(() => {});
        }
    }
}

document.addEventListener('mousedown', clickHandler);
document.addEventListener('touchstart', clickHandler, { passive: true });

export function init(id, dotnet) {
    drawers.set(id, { dotnet: dotnet, isOpen: false, isPinned: false });
}

export function updateState(id, isOpen, isPinned) {
    const state = drawers.get(id);
    if (state) {
        state.isOpen = isOpen;
        state.isPinned = isPinned;
    }
}

export function dispose(id) {
    drawers.delete(id);
}
