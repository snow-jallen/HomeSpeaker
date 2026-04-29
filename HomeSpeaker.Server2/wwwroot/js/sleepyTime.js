// sleepyTime.js — idle detection for the sleepy-time overlay
// Uses IIFE pattern (matching keyboard.js) so it attaches to window.
window.sleepyTime = (function () {
    let _dotnet = null;

    function resetIdle() {
        if (_dotnet) {
            _dotnet.invokeMethodAsync('OnUserActivity').catch(() => {});
        }
    }

    return {
        startWatching: function (dotnetRef) {
            if (_dotnet) return; // already watching
            _dotnet = dotnetRef;
            document.addEventListener('pointerdown', resetIdle);
            document.addEventListener('keydown', resetIdle);
        },

        stopWatching: function () {
            document.removeEventListener('pointerdown', resetIdle);
            document.removeEventListener('keydown', resetIdle);
            _dotnet = null;
        }
    };
})();
