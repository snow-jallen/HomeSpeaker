// ambientSlideshow.js - the ambient photo/video slideshow inside .hs-ambient.
// Items come from /api/photos/next ({ id, url, place, when, layout, kind, durationMs }).
// Photos crossfade between two <img class="hs-slide"> layers; videos play muted in a third
// <video class="hs-slide hs-slide-video"> layer. A short history lets the user swipe back and
// forward. Advances only while idleController is in 'ambient' mode: outside it no timer runs and the
// blurred backdrop stays on the last photo. On the first init it seeds that backdrop on every device
// (phones and laptops never enter ambient mode), skipping past videos until a photo arrives.
window.ambientSlideshow = (function () {
    const maxHistory = 20;
    const videoLoadTimeoutMs = 10000;
    const maxConsecutiveSkips = 3;
    const videoStallGraceMs = 5000;
    const fadeMs = 1600; // a little longer than the 1.5s CSS opacity transition
    const maxSeedAttempts = 5;

    let slideMs = 30000;
    let maxVideoMs = 60000;
    let timer = null;
    let unloadTimer = null;
    let front = 0;
    let busy = false;
    let initialized = false;
    let history = [];
    let pos = -1;
    let showToken = 0; // bumped by every show(); a superseded show aborts after its awaits
    let navGen = 0;    // bumped by navigation; a /next response that arrives late is dropped
    let videoDisposers = [];
    let consecutiveSkips = 0;
    let lastDirection = 'forward'; // a broken video is skipped in the direction the user was heading

    function setText(selector, text) {
        const el = document.querySelector(selector);
        if (el) el.textContent = text;
    }

    function isAmbient() {
        return window.idleController.mode() === 'ambient';
    }

    function imgSlides() { return document.querySelectorAll('img.hs-slide'); }
    function videoEl() { return document.querySelector('video.hs-slide-video'); }

    function clearTimer() {
        clearTimeout(timer);
        timer = null;
    }

    // Timers only run while the slideshow is on screen.
    function schedule(fn, ms) {
        clearTimer();
        if (!isAmbient()) return;
        timer = setTimeout(fn, ms);
    }

    function disposeVideoListeners() {
        const disposers = videoDisposers;
        videoDisposers = [];
        disposers.forEach(function (dispose) { dispose(); });
    }

    function cancelUnload() {
        clearTimeout(unloadTimer);
        unloadTimer = null;
    }

    function unloadVideo() {
        cancelUnload();
        const video = videoEl();
        if (!video) return;
        video.pause();
        video.removeAttribute('src');
        video.load(); // releases the stream connection
    }

    // Pause now (the last frame fades out) but only drop the stream once the fade has finished.
    function releaseVideoSoon() {
        const video = videoEl();
        if (!video) return;
        disposeVideoListeners();
        video.pause();
        if (!video.hasAttribute('src')) return;
        cancelUnload();
        unloadTimer = setTimeout(unloadVideo, fadeMs);
    }

    function clearPhotos() {
        showToken++;
        document.querySelectorAll('.hs-slide').forEach(function (el) { el.classList.remove('is-visible'); });
        document.documentElement.style.setProperty('--hs-photo', 'none');
        setText('.hs-caption-place', '');
        setText('.hs-caption-when', '');
        disposeVideoListeners();
        unloadVideo();
        history = [];
        pos = -1;
    }

    function applyMeta(item) {
        const overlay = document.querySelector('.hs-ambient-overlay');
        if (overlay) overlay.dataset.layout = item.layout || 'default';
        setText('.hs-caption-place', item.place || '');
        setText('.hs-caption-when', item.when || '');
    }

    function isVideo(item) {
        return item && item.kind === 'video';
    }

    // How long a video may play: its own duration, capped by maxVideoSeconds. A null or zero
    // duration (Immich reports 0 for some assets) means "use the cap".
    function videoLimitMs(item) {
        const durationMs = Number(item.durationMs);
        return durationMs > 0 ? Math.min(durationMs, maxVideoMs) : maxVideoMs;
    }

    function waitForCanPlay(video) {
        return new Promise(function (resolve) {
            let done = false;
            let reason = null;
            let t = null;
            function finish(ok, why) {
                if (done) return;
                done = true;
                reason = why;
                clearTimeout(t);
                video.removeEventListener('canplay', onCanPlay);
                video.removeEventListener('error', onError);
                resolve({ ok: ok, reason: reason });
            }
            function onCanPlay() { finish(true, 'canplay'); }
            function onError() { finish(false, 'error'); }
            video.addEventListener('canplay', onCanPlay);
            video.addEventListener('error', onError);
            t = setTimeout(function () { finish(false, 'timeout'); }, videoLoadTimeoutMs);
            videoDisposers.push(function () { finish(false, 'cancelled'); });
        });
    }

    function skipVideo(item, why) {
        console.debug('ambientSlideshow: skipping video', item && item.id, why);
        disposeVideoListeners();
        unloadVideo();
        // Several broken videos in a row must not turn into a tight request loop.
        if (++consecutiveSkips >= maxConsecutiveSkips) {
            consecutiveSkips = 0;
            schedule(next, slideMs);
            return;
        }
        if (lastDirection === 'back' && pos > 0) previous();
        else next();
    }

    async function showPhoto(item, token) {
        const slides = imgSlides();
        if (slides.length < 2) return;
        const incoming = slides[1 - front];
        incoming.src = item.url;
        // If decode() never settles the timer would stay cleared and the slideshow would freeze.
        schedule(next, slideMs);
        await incoming.decode();
        if (token !== showToken) return false;

        document.documentElement.style.setProperty('--hs-photo', 'url("' + item.url + '")');
        applyMeta(item);

        consecutiveSkips = 0;
        incoming.classList.add('is-visible');
        slides[front].classList.remove('is-visible');
        front = 1 - front;
        const video = videoEl();
        if (video) video.classList.remove('is-visible');
        releaseVideoSoon();

        schedule(next, slideMs);
        return true;
    }

    async function showVideo(item, token) {
        const video = videoEl();
        if (!video) return;

        // Outside ambient mode the video stays in the history but never plays.
        if (!isAmbient()) return false;

        cancelUnload();
        video.muted = true; // Blazor's static `muted` attribute does not set the property
        video.src = item.url;

        const loaded = await waitForCanPlay(video);
        if (token !== showToken) return;
        if (loaded.reason === 'cancelled') return; // the user left ambient mode mid-load
        if (!loaded.ok) { skipVideo(item, loaded.reason); return; }
        if (!isAmbient()) return;

        video.currentTime = 0;
        // Same safety net for a play() that never settles; the normal schedule replaces it.
        schedule(next, maxVideoMs + videoStallGraceMs);
        try {
            await video.play();
        } catch (e) {
            if (token !== showToken) return;
            skipVideo(item, e);
            return;
        }
        if (token !== showToken) return;
        if (!isAmbient()) { video.pause(); return; }

        consecutiveSkips = 0;
        applyMeta(item);
        video.classList.add('is-visible');
        imgSlides().forEach(function (img) { img.classList.remove('is-visible'); });

        function onEnded() { next(); }
        function onError() { skipVideo(item, 'playback error'); }
        video.addEventListener('ended', onEnded);
        video.addEventListener('error', onError);
        videoDisposers.push(function () {
            video.removeEventListener('ended', onEnded);
            video.removeEventListener('error', onError);
        });

        schedule(next, videoLimitMs(item));
        return true;
    }

    // Resolves true once the item is on screen (for a photo, once it is also the backdrop).
    async function show(item) {
        const token = ++showToken;
        clearTimer();
        disposeVideoListeners();
        try {
            if (isVideo(item)) return await showVideo(item, token) === true;
            return await showPhoto(item, token) === true;
        } catch (e) {
            if (token !== showToken) return false;
            console.debug('ambientSlideshow: keeping current item', e);
            schedule(next, slideMs);
            return false;
        }
    }

    // Fetches the next item from the server. Returns null when there is nothing to show; the
    // retry is already scheduled in that case.
    async function fetchNext() {
        if (busy) return null;
        busy = true;
        const gen = navGen;
        try {
            const resp = await fetch('/api/photos/next', { cache: 'no-store', signal: AbortSignal.timeout(15000) });
            // The user navigated while this was in flight: whatever came back is stale, so it
            // must not clear the photos, re-arm the timer or add to the history.
            if (gen !== navGen) return null;
            if (resp.status === 204) {
                clearPhotos();
                schedule(next, slideMs);
                return null;
            }
            if (!resp.ok) { schedule(next, slideMs); return null; } // keep the current item; retry later
            const item = await resp.json();
            if (gen !== navGen) return null;
            if (!item || !item.url) { schedule(next, slideMs); return null; }
            return item;
        } catch (e) {
            console.debug('ambientSlideshow: keeping current item', e);
            if (gen === navGen) schedule(next, slideMs);
            return null;
        } finally {
            busy = false;
        }
    }

    function remember(item) {
        history.push(item);
        while (history.length > maxHistory) history.shift();
        pos = history.length - 1;
    }

    // Gives the blurred backdrop a photo on every device. Outside ambient mode a video never plays
    // (so it would leave the page black): keep it in the history and fetch again, up to
    // maxSeedAttempts items. Once ambient mode is running, the normal slideshow takes over.
    async function seedBackdrop() {
        for (let attempt = 0; attempt < maxSeedAttempts; attempt++) {
            const item = await fetchNext();
            if (!item) return;
            remember(item);
            const shown = await show(item);
            if (shown || isAmbient()) return;
        }
    }

    async function next() {
        if (pos >= 0 && pos < history.length - 1) {
            // Replaying forward after the user went back.
            navGen++;
            lastDirection = 'forward';
            pos++;
            await show(history[pos]);
            return;
        }
        lastDirection = 'forward';
        const item = await fetchNext();
        if (!item) return;
        remember(item);
        await show(item);
    }

    // Re-arms the timer for the item already on screen.
    function restartTimer() {
        const item = history[pos];
        if (!item) { next(); return; }
        if (isVideo(item)) {
            const video = videoEl();
            const played = video ? video.currentTime * 1000 : 0;
            schedule(next, Math.max(500, videoLimitMs(item) - played));
        } else {
            schedule(next, slideMs);
        }
    }

    async function previous() {
        navGen++;
        if (pos > 0) {
            lastDirection = 'back';
            pos--;
            await show(history[pos]);
        } else {
            restartTimer();
        }
    }

    function onModeChange(mode) {
        if (mode === 'ambient') {
            lastDirection = 'forward';
            if (pos >= 0 && history[pos]) show(history[pos]);
            else next();
        } else {
            // Leave the current image up: it is the blurred backdrop for the normal UI.
            showToken++; // abandon any show still loading or decoding
            clearTimer();
            disposeVideoListeners();
            const video = videoEl();
            if (video) {
                video.pause();
                // A video that was still loading never became visible: drop its stream.
                if (!video.classList.contains('is-visible')) unloadVideo();
            }
        }
    }

    function applyOptions(options) {
        slideMs = Math.max(5, (options && options.slideSeconds) || 30) * 1000;
        maxVideoMs = Math.max(1, (options && options.maxVideoSeconds) || 60) * 1000;
    }

    return {
        init: function (options) {
            applyOptions(options);
            if (initialized) return;
            initialized = true;
            window.idleController.onModeChange(onModeChange);
            if (typeof window.idleController.onSwipe === 'function') {
                window.idleController.onSwipe(function (direction) {
                    if (direction === 'left') next();
                    else previous();
                });
            }
            seedBackdrop();
        },
        next: next,
        previous: previous
    };
})();
