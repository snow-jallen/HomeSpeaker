// clipPlayer.js - plays short clips in the browser for the playlist suggestions page, and its keyboard shortcuts.
// A clip starts a third of the way into the song (past most intros) and stops after CLIP_SECONDS. The page is told
// when a clip stops on its own (OnClipEnded) and about shortcut keys (OnShortcut): space plays or stops the clip,
// Y approves, N rejects, U undoes, and the right arrow jumps further into the song.
window.clipPlayer = (function () {
    const CLIP_SECONDS = 30;
    const START_FRACTION = 1 / 3;
    const JUMP_SECONDS = 20;
    let audio = null;
    let stopTimer = null;
    let dotNetRef = null;
    let keyHandler = null;
    let playingId = null;

    function notifyEnded(id) {
        if (dotNetRef && id !== null) {
            dotNetRef.invokeMethodAsync('OnClipEnded', id).catch(() => { });
        }
    }

    function stop() {
        clearTimeout(stopTimer);
        stopTimer = null;
        if (audio) {
            audio.pause();
            audio.removeAttribute('src');
            audio.load();
            audio = null;
        }
        playingId = null;
    }

    function armStopTimer(id) {
        clearTimeout(stopTimer);
        stopTimer = setTimeout(() => {
            if (playingId === id) {
                stop();
                notifyEnded(id);
            }
        }, CLIP_SECONDS * 1000);
    }

    async function play(url, id) {
        stop();
        const clip = new Audio();
        clip.preload = 'auto';
        audio = clip;
        playingId = id;
        clip.addEventListener('loadedmetadata', () => {
            if (isFinite(clip.duration) && clip.duration > CLIP_SECONDS * 2) {
                clip.currentTime = clip.duration * START_FRACTION;
            }
        }, { once: true });
        clip.addEventListener('ended', () => {
            if (audio === clip) {
                stop();
                notifyEnded(id);
            }
        });
        clip.src = url;
        try {
            await clip.play();
            armStopTimer(id);
            return true;
        } catch (e) {
            // Autoplay blocked (no tap yet) or the file failed: the page shows it as not playing.
            if (audio === clip) stop();
            return false;
        }
    }

    function jump() {
        if (!audio || !isFinite(audio.duration)) return;
        audio.currentTime = Math.min(audio.duration - 1, audio.currentTime + JUMP_SECONDS);
        armStopTimer(playingId);
    }

    function attach(ref) {
        detach();
        dotNetRef = ref;
        keyHandler = (e) => {
            const target = e.target;
            if (e.ctrlKey || e.metaKey || e.altKey) return;
            if (target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName))) return;
            let key = null;
            if (e.key === ' ') key = 'play';
            else if (e.key === 'y' || e.key === 'Y') key = 'approve';
            else if (e.key === 'n' || e.key === 'N') key = 'reject';
            else if (e.key === 'u' || e.key === 'U') key = 'undo';
            else if (e.key === 'ArrowRight') { jump(); e.preventDefault(); return; }
            if (!key) return;
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnShortcut', key).catch(() => { });
        };
        document.addEventListener('keydown', keyHandler);
    }

    function detach() {
        stop();
        if (keyHandler) document.removeEventListener('keydown', keyHandler);
        keyHandler = null;
        dotNetRef = null;
    }

    return { play, stop, jump, attach, detach };
})();
