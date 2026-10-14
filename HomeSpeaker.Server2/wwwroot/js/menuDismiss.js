// menuDismiss.js - closes a popup menu when the user taps or presses outside it, or presses Escape.
// dismissAll() closes every open menu at once (idleController uses it when the screen goes ambient or night).
// Listens on the document, so it does not depend on any container's stacking context
// (a position:fixed click-catcher is clipped by ancestors that use backdrop-filter).
window.menuDismiss = (function () {
    const active = new Map(); // anchor id -> { onPointerDown, onKeyDown, requestClose }

    function detach(anchorId) {
        const entry = active.get(anchorId);
        if (!entry) return;
        document.removeEventListener('pointerdown', entry.onPointerDown, true);
        document.removeEventListener('keydown', entry.onKeyDown, true);
        active.delete(anchorId);
    }

    function attach(anchorId, dotNetRef) {
        detach(anchorId);
        const requestClose = function () {
            dotNetRef.invokeMethodAsync('CloseFromOutside').catch(function () { /* circuit gone */ });
        };
        const entry = {
            requestClose: requestClose,
            onPointerDown: function (e) {
                const anchor = document.getElementById(anchorId);
                if (anchor && e.target instanceof Node && anchor.contains(e.target)) return;
                requestClose();
            },
            onKeyDown: function (e) {
                if (e.key === 'Escape') requestClose();
            }
        };
        document.addEventListener('pointerdown', entry.onPointerDown, true);
        document.addEventListener('keydown', entry.onKeyDown, true);
        active.set(anchorId, entry);
    }

    // The component detaches itself once it has closed the menu, so iterate over a copy.
    function dismissAll() {
        Array.from(active.values()).forEach(function (entry) { entry.requestClose(); });
    }

    return { attach: attach, detach: detach, dismissAll: dismissAll };
})();
