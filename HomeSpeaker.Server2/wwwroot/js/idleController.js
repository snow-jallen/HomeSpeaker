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

    function isKiosk() {
        return document.body && document.body.dataset.kiosk === 'true';
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
            setMode(quiet ? 'night' : 'ambient');
        }, quiet ? nightIdleMs : idleMs);
    }

    // The tap that wakes the screen must not also press whatever control is underneath.
    function onActivity(e) {
        if (mode !== 'active') {
            e.preventDefault();
            e.stopPropagation();
            if (e.type === 'pointerdown') {
                swallowClick = true;
                setTimeout(function () { swallowClick = false; }, 700);
            }
            setMode('active');
        }
        arm();
    }

    function onClick(e) {
        if (swallowClick) {
            swallowClick = false;
            e.preventDefault();
            e.stopPropagation();
        }
    }

    document.addEventListener('pointerdown', onActivity, true);
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
        onModeChange: function (callback) { listeners.push(callback); }
    };
})();
