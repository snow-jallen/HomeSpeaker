// ambientSlideshow.js - the ambient photo/video slideshow inside .hs-ambient.
// Items come from /api/photos/next ({ id, url, place, when, layout, kind, durationMs }).
// Photos crossfade between two <img class="hs-slide"> layers; videos play muted in a third
// <video class="hs-slide hs-slide-video"> layer. A short history lets the user swipe back and
// forward. Advances only while idleController is in 'ambient' mode: outside it no timer runs and the
// blurred backdrop stays on the last photo. On the first init it seeds that backdrop on every device
// (phones and laptops never enter ambient mode), skipping past videos until a photo arrives. The
// photo API has no still image for a video, so if every seeded item is a video it tries once more
// after seedRetryMs (a single one-shot timer, never a repeating one).
// A tall photo comes with a `portrait` style the server picked at random (see PortraitStyle.cs):
// 'blur' and 'pair' are drawn once into a canvas and shown as a blob: URL, so the Pi never runs a
// live blur; 'faces' shifts the cover crop onto the faces; 'pan' slides the photo from top to bottom.
window.ambientSlideshow = (function () {
    const maxHistory = 20;
    const videoLoadTimeoutMs = 10000;
    const maxConsecutiveSkips = 3;
    const videoStallGraceMs = 5000;
    const fadeMs = 1600; // a little longer than the 1.5s CSS opacity transition
    const maxSeedAttempts = 5;
    const seedRetryMs = 60000;
    const tallAspectLimit = 1.2; // matches PhotoLibrary.TallAspectLimit
    const faceAnchor = 0.4;      // faces sit 40% of the way down the visible area, leaving headroom
    const noFaceAnchor = 0.3;    // without faces, crop toward the top, where heads usually are
    const pairGapPx = 6;

    let slideMs = 30000;
    let maxVideoMs = 60000;
    let timer = null;
    let unloadTimer = null;
    let seedRetryTimer = null;
    let backdropReady = false; // a photo has been shown, so the blurred backdrop is set
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
    const blobUrls = new WeakMap(); // img -> the composed blob: URL it shows, revoked when the img is reused

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
        backdropReady = false;
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

    function loadImage(url) {
        const img = new Image();
        img.src = url;
        return img.decode().then(function () { return img; });
    }

    function isTall(img) {
        return img.naturalWidth < img.naturalHeight * tallAspectLimit;
    }

    // Where a crop of `visible` out of `full` (both 0..1 of the image) should start so the faces sit at
    // faceAnchor of the visible span. `axis` is 'y' or 'x'.
    function cropStart(faces, axis, visible, fallbackAnchor) {
        const room = 1 - visible;
        if (room <= 0) return 0;
        let start;
        if (faces && faces.length) {
            const lo = Math.min.apply(null, faces.map(function (f) { return axis === 'y' ? f.y1 : f.x1; }));
            const hi = Math.max.apply(null, faces.map(function (f) { return axis === 'y' ? f.y2 : f.x2; }));
            const anchor = axis === 'y' ? faceAnchor : 0.5;
            start = (lo + hi) / 2 - visible * anchor;
        } else {
            start = room * fallbackAnchor;
        }
        return Math.min(room, Math.max(0, start));
    }

    // Draws `img` to fill the box, cropping around its faces.
    function drawCover(ctx, img, faces, x, y, w, h) {
        const nw = img.naturalWidth, nh = img.naturalHeight;
        const boxAspect = w / h;
        if (nw / nh < boxAspect) {
            const visible = (nw / boxAspect) / nh;
            const sy = cropStart(faces, 'y', visible, noFaceAnchor) * nh;
            ctx.drawImage(img, 0, sy, nw, visible * nh, x, y, w, h);
        } else {
            const visible = (nh * boxAspect) / nw;
            const sx = cropStart(faces, 'x', visible, 0.5) * nw;
            ctx.drawImage(img, sx, 0, visible * nw, nh, x, y, w, h);
        }
    }

    // The whole photo, centered over a blurred, darkened copy of itself. The blur is done once on a
    // tiny canvas and scaled up, which is cheap and smooth.
    function drawBlurred(ctx, img, w, h) {
        const small = document.createElement('canvas');
        small.width = 96;
        small.height = Math.max(1, Math.round(96 * h / w));
        const sctx = small.getContext('2d');
        sctx.filter = 'blur(3px)';
        // Overscan so the blur doesn't pull transparent edges in.
        const pad = 8;
        drawCover(sctx, img, null, -pad, -pad, small.width + pad * 2, small.height + pad * 2);
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(small, 0, 0, w, h);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.35)';
        ctx.fillRect(0, 0, w, h);

        const scale = Math.min(w / img.naturalWidth, h / img.naturalHeight);
        const dw = img.naturalWidth * scale, dh = img.naturalHeight * scale;
        ctx.shadowColor = 'rgba(0, 0, 0, 0.6)';
        ctx.shadowBlur = Math.round(w / 40);
        ctx.drawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
        ctx.shadowColor = 'transparent';
    }

    // Composes a 'blur' or 'pair' slide at screen resolution. Resolves to a blob: URL, or null when the
    // photo turned out not to be tall (the plain cover crop is then fine).
    async function composeFrame(item) {
        const box = document.querySelector('.hs-ambient');
        const dpr = Math.min(window.devicePixelRatio || 1, 2);
        const w = Math.round(((box && box.clientWidth) || window.innerWidth) * dpr);
        const h = Math.round(((box && box.clientHeight) || window.innerHeight) * dpr);
        const urls = [item.url];
        if (item.portrait === 'pair' && item.partner) urls.push(item.partner.url);
        const images = await Promise.all(urls.map(loadImage));
        if (!isTall(images[0]) || w < 2 || h < 2) return null;

        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        if (images.length === 2) {
            const gap = Math.round(pairGapPx * dpr);
            const half = (w - gap) / 2;
            ctx.fillStyle = '#111';
            ctx.fillRect(0, 0, w, h);
            drawCover(ctx, images[0], item.faces, 0, 0, half, h);
            drawCover(ctx, images[1], item.partner.faces, half + gap, 0, half, h);
        } else {
            drawBlurred(ctx, images[0], w, h);
        }
        const blob = await new Promise(function (resolve) { canvas.toBlob(resolve, 'image/jpeg', 0.9); });
        return blob ? URL.createObjectURL(blob) : null;
    }

    function setSlideSrc(img, src) {
        const old = blobUrls.get(img);
        if (old && old !== src) URL.revokeObjectURL(old);
        if (src.startsWith('blob:')) blobUrls.set(img, src);
        else blobUrls.delete(img);
        img.src = src;
    }

    function resetPortraitStyle(img) {
        img.getAnimations().forEach(function (a) { a.cancel(); });
        img.classList.remove('is-pan');
        img.style.objectPosition = '';
    }

    // 'faces' and 'pan' work on the decoded <img> itself. Returns false when the photo isn't tall after all.
    function applyImgStyle(img, item, style) {
        if (!isTall(img)) return false;
        const box = document.querySelector('.hs-ambient');
        const bw = (box && box.clientWidth) || window.innerWidth;
        const bh = (box && box.clientHeight) || window.innerHeight;
        const scaledHeight = bw * img.naturalHeight / img.naturalWidth;
        const visible = bh / scaledHeight;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (style === 'pan' && !reduceMotion && visible < 1) {
            img.classList.add('is-pan');
            img.animate(
                [{ transform: 'translateY(0)' }, { transform: 'translateY(' + (bh - scaledHeight) + 'px)' }],
                { duration: slideMs + fadeMs, easing: 'ease-in-out', fill: 'forwards' });
        } else {
            const room = 1 - visible;
            const start = cropStart(item.faces, 'y', visible, noFaceAnchor);
            img.style.objectPosition = '50% ' + (room > 0 ? (start / room) * 100 : 50) + '%';
        }
        return true;
    }

    async function showPhoto(item, token) {
        const slides = imgSlides();
        if (slides.length < 2) return false;
        const incoming = slides[1 - front];
        // If decode() never settles the timer would stay cleared and the slideshow would freeze.
        schedule(next, slideMs);

        // Portrait styles only matter on screen; the backdrop for the normal UI is always the plain photo.
        const style = isAmbient() ? item.portrait : null;
        let src = item.url;
        if (style === 'blur' || style === 'pair') {
            try {
                src = (await composeFrame(item)) || item.url;
            } catch (e) {
                console.debug('ambientSlideshow: showing portrait uncomposed', e);
            }
            if (token !== showToken) {
                if (src !== item.url) URL.revokeObjectURL(src);
                return false;
            }
        }
        resetPortraitStyle(incoming);
        setSlideSrc(incoming, src);
        await incoming.decode();
        if (token !== showToken) return false;
        if (style === 'faces' || style === 'pan') applyImgStyle(incoming, item, style);
        if (style) console.debug('ambientSlideshow: portrait style', style, item.id);

        document.documentElement.style.setProperty('--hs-photo', 'url("' + item.url + '")');
        backdropReady = true;
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
        if (!video) return false;

        // Outside ambient mode the video stays in the history but never plays.
        if (!isAmbient()) return false;

        cancelUnload();
        video.muted = true; // Blazor's static `muted` attribute does not set the property
        video.src = item.url;

        const loaded = await waitForCanPlay(video);
        if (token !== showToken) return false;
        if (loaded.reason === 'cancelled') return false; // the user left ambient mode mid-load
        if (!loaded.ok) { skipVideo(item, loaded.reason); return false; }
        if (!isAmbient()) return false;

        video.currentTime = 0;
        // Same safety net for a play() that never settles; the normal schedule replaces it.
        schedule(next, maxVideoMs + videoStallGraceMs);
        try {
            await video.play();
        } catch (e) {
            if (token !== showToken) return false;
            skipVideo(item, e);
            return false;
        }
        if (token !== showToken) return false;
        if (!isAmbient()) { video.pause(); return false; }

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
            return isVideo(item) ? await showVideo(item, token) : await showPhoto(item, token);
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
    // maxSeedAttempts items. Once ambient mode is running, the normal slideshow takes over. If every
    // item was a video, retry once after seedRetryMs (retry = false for that second pass).
    async function seedBackdrop(retry) {
        for (let attempt = 0; attempt < maxSeedAttempts; attempt++) {
            const item = await fetchNext();
            if (!item) return;
            remember(item);
            const shown = await show(item);
            if (shown || isAmbient()) return;
        }
        if (retry) {
            clearTimeout(seedRetryTimer);
            seedRetryTimer = setTimeout(function () {
                seedRetryTimer = null;
                if (!backdropReady && !isAmbient()) seedBackdrop(false);
            }, seedRetryMs);
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
            // The slideshow itself now supplies photos, so the pending backdrop retry is unnecessary.
            clearTimeout(seedRetryTimer);
            seedRetryTimer = null;
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
            seedBackdrop(true);
        },
        next: next,
        previous: previous
    };
})();
