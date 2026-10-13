// ambientSlideshow.js - crossfades between two <img class="hs-slide"> layers inside .hs-ambient.
// Photos come from /api/photos/next. Advances only while idleController is in 'ambient' mode;
// on any page load it fetches one photo so the shell's blurred backdrop has something to show.
window.ambientSlideshow = (function () {
    let slideMs = 30000;
    let timer = null;
    let front = 0;
    let busy = false;

    function setText(selector, text) {
        const el = document.querySelector(selector);
        if (el) el.textContent = text;
    }

    function clearPhotos() {
        document.querySelectorAll('.hs-slide').forEach(function (img) { img.classList.remove('is-visible'); });
        document.documentElement.style.setProperty('--hs-photo', 'none');
        setText('.hs-caption-place', '');
        setText('.hs-caption-when', '');
    }

    async function advance() {
        if (busy) return;
        busy = true;
        try {
            const resp = await fetch('/api/photos/next', { cache: 'no-store', signal: AbortSignal.timeout(15000) });
            if (resp.status === 204) { clearPhotos(); return; }
            if (!resp.ok) return; // keep the current photo; try again next tick
            const photo = await resp.json();

            const slides = document.querySelectorAll('.hs-slide');
            if (slides.length < 2) return;
            const incoming = slides[1 - front];
            incoming.src = photo.url;
            await incoming.decode();

            document.documentElement.style.setProperty('--hs-photo', 'url("' + photo.url + '")');
            const overlay = document.querySelector('.hs-ambient-overlay');
            if (overlay) overlay.dataset.layout = photo.layout || 'default';
            setText('.hs-caption-place', photo.place || '');
            setText('.hs-caption-when', photo.when || '');

            incoming.classList.add('is-visible');
            slides[front].classList.remove('is-visible');
            front = 1 - front;
        } catch (e) {
            console.debug('ambientSlideshow: keeping current photo', e);
        } finally {
            busy = false;
        }
    }

    function onModeChange(mode) {
        clearInterval(timer);
        timer = null;
        if (mode === 'ambient') {
            advance();
            timer = setInterval(advance, slideMs);
        }
    }

    return {
        init: function (options) {
            slideMs = Math.max(5, (options && options.slideSeconds) || 30) * 1000;
            advance();
            window.idleController.onModeChange(onModeChange);
        }
    };
})();
