// idleController.js - kiosk screen modes: 'active' (normal UI), 'ambient' (photo slideshow),
// 'night' (dim clock). Pure DOM, so it keeps working even if the Blazor circuit drops.
// Only browsers marked <body data-kiosk="true"> ever leave 'active' on their own.
window.idleController = (function () {
    const nightIdleMs = 60000;
    let mode = 'active';
    let quiet = false;
    let idleMs = 120000;
    let timer = null;
    let swallowClick = false;
    const listeners = [];
    const swipeListeners = [];
    const swipeMinPx = 60;

    let swallowTimer = null;
    let gesture = null; // ambient-mode touch in progress: { id, x, y }

    function isKiosk() {
        return !!document.body && document.body.dataset.kiosk === 'true';
    }

    function clearSwallow() {
        swallowClick = false;
        clearTimeout(swallowTimer);
        swallowTimer = null;
    }

    function setMode(next) {
        if (next === mode) return;
        mode = next;
        const root = document.documentElement;
        root.classList.toggle('hs-mode-ambient', mode === 'ambient');
        root.classList.toggle('hs-mode-night', mode === 'night');
        listeners.forEach(function (callback) {
            try { callback(mode); } catch (e) { console.error('idleController listener failed', e); }
        });
    }

    function arm() {
        clearTimeout(timer);
        if (!isKiosk()) return;
        timer = setTimeout(function () {
            // NoTopLayout pages (/nightscout, /aspire) have no ambient or night layers: go home so the kiosk can dim.
            if (!document.querySelector('.hs-ambient')) {
                location.href = '/';
                return;
            }
            setMode(quiet ? 'night' : 'ambient');
        }, quiet ? nightIdleMs : idleMs);
    }

    // Swallow the click that ends this gesture, however long the finger stays down. The timeout
    // is only a fallback for gestures that never produce a click.
    function armSwallow() {
        swallowClick = true;
        clearTimeout(swallowTimer);
        swallowTimer = setTimeout(clearSwallow, 1500);
    }

    // The tap that wakes the screen must not also press whatever control is underneath.
    function onActivity(e) {
        // A swipe-wake never produces a click, so clear its stale swallow flag on the next touch.
        if (mode === 'active') clearSwallow();
        if (mode !== 'active') {
            e.preventDefault();
            e.stopPropagation();
            if (e.type === 'pointerdown') armSwallow();
            setMode('active');
        }
        arm();
    }

    // Ambient mode tells a swipe (browse photos) from a tap (wake) by waiting for pointerup.
    // Night mode and active mode keep their immediate behaviour.
    function onPointerDown(e) {
        if (mode !== 'ambient') {
            onActivity(e);
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        if (gesture && gesture.id !== e.pointerId) return; // ignore a second finger
        clearSwallow();
        gesture = { id: e.pointerId, x: e.clientX, y: e.clientY };
    }

    function onPointerUp(e) {
        if (!gesture || gesture.id !== e.pointerId) return;
        const start = gesture;
        gesture = null;
        e.preventDefault();
        e.stopPropagation();
        const dx = e.clientX - start.x;
        const dy = e.clientY - start.y;
        armSwallow();
        if (mode === 'ambient' && Math.abs(dx) >= swipeMinPx && Math.abs(dx) > Math.abs(dy)) {
            const direction = dx < 0 ? 'left' : 'right';
            swipeListeners.forEach(function (callback) {
                try { callback(direction); } catch (err) { console.error('idleController swipe handler failed', err); }
            });
            return;
        }
        setMode('active');
        arm();
    }

    function onPointerCancel() {
        gesture = null;
        clearSwallow();
    }

    function onClick(e) {
        if (swallowClick) {
            clearSwallow();
            e.preventDefault();
            e.stopPropagation();
        }
    }

    document.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('pointerup', onPointerUp, true);
    document.addEventListener('pointercancel', onPointerCancel, true);
    document.addEventListener('keydown', onActivity, true);
    document.addEventListener('click', onClick, true);

    return {
        init: function (options) {
            idleMs = Math.max(10, (options && options.idleSeconds) || 120) * 1000;
            arm();
        },
        setQuiet: function (isQuiet) {
            if (isQuiet === quiet) return;
            quiet = isQuiet;
            if (mode === 'ambient' && quiet) setMode('night');
            else if (mode === 'night' && !quiet) setMode('ambient');
            arm();
        },
        enter: function (requested) {
            setMode(requested === 'night' ? 'night' : 'ambient');
        },
        mode: function () { return mode; },
        isKiosk: isKiosk,
        onModeChange: function (callback) { listeners.push(callback); },
        onSwipe: function (callback) { swipeListeners.push(callback); }
    };
})();
