// Remembers which anchor person this browser belongs to, so "Anchors" can open straight to your own day.
// Shared screens (the kitchen kiosk) never remember anyone.
window.hsAnchorMe = (() => {
    const key = 'hs-anchor-me';

    function isKiosk() {
        return document.body?.dataset.kiosk === 'true';
    }

    function get() {
        if (isKiosk()) return null;
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    }

    function set(id) {
        if (isKiosk()) return;
        try {
            localStorage.setItem(key, id);
        } catch {
            // Storage blocked (private mode): the choice just isn't remembered.
        }
    }

    function clear() {
        try {
            localStorage.removeItem(key);
        } catch {
            // Nothing to clear.
        }
    }

    return { get, set, clear, isKiosk };
})();
