// audioPlayer.js - Browser-based audio player for HomeSpeaker
let audioElement = null;
let dotNetHelper = null;
let currentSong = null;

export function initialize(dotNetReference) {
    dotNetHelper = dotNetReference;
    
    if (!audioElement) {
        audioElement = new Audio();
        audioElement.preload = 'metadata';
        
        // Set up event listeners
        audioElement.addEventListener('loadedmetadata', () => {
            notifyStatusChanged();
        });
        
        audioElement.addEventListener('timeupdate', () => {
            notifyStatusChanged();
        });
        
        audioElement.addEventListener('play', () => {
            notifyStatusChanged();
        });
        
        audioElement.addEventListener('pause', () => {
            notifyStatusChanged();
        });
        
        audioElement.addEventListener('ended', () => {
            notifyStatusChanged();
        });
        
        audioElement.addEventListener('error', (e) => {
            const error = `Audio error: ${e.error?.message || 'Unknown error'}`;
            console.error(error, e);
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnError', error);
            }
        });
        
        audioElement.addEventListener('volumechange', () => {
            notifyStatusChanged();
        });
    }
}

export function playSong(url, songName) {
    if (!audioElement) {
        console.error('Audio player not initialized');
        return;
    }
    
    currentSong = songName;
    audioElement.src = url;
    audioElement.load();
    
    // Try to play, handling potential autoplay restrictions
    const playPromise = audioElement.play();
    
    if (playPromise !== undefined) {
        playPromise
            .then(() => {
                console.log('Audio playback started successfully');
            })
            .catch(error => {
                console.warn('Autoplay prevented, user interaction required:', error);
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnError', 
                        'Playback blocked by browser. Click to enable audio.');
                }
            });
    }
}

export function pause() {
    if (audioElement && !audioElement.paused) {
        audioElement.pause();
    }
}

export function resume() {
    if (audioElement && audioElement.paused) {
        const playPromise = audioElement.play();
        
        if (playPromise !== undefined) {
            playPromise.catch(error => {
                console.error('Resume failed:', error);
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnError', `Resume failed: ${error.message}`);
                }
            });
        }
    }
}

export function stop() {
    if (audioElement) {
        audioElement.pause();
        audioElement.currentTime = 0;
        currentSong = null;
    }
}

export function setVolume(volume) {
    if (audioElement) {
        audioElement.volume = Math.max(0, Math.min(1, volume));
    }
}

export function getVolume() {
    return audioElement ? audioElement.volume : 0.5;
}

export function seekTo(seconds) {
    if (audioElement && !isNaN(audioElement.duration)) {
        audioElement.currentTime = Math.max(0, Math.min(seconds, audioElement.duration));
    }
}

export function getStatus() {
    if (!audioElement) {
        return {
            isPlaying: false,
            isPaused: false,
            currentTime: 0,
            duration: 0,
            volume: 0.5,
            currentSong: null
        };
    }
    
    return {
        isPlaying: !audioElement.paused && !audioElement.ended && audioElement.currentTime > 0,
        isPaused: audioElement.paused,
        currentTime: audioElement.currentTime || 0,
        duration: audioElement.duration || 0,
        volume: audioElement.volume,
        currentSong: currentSong
    };
}

export function dispose() {
    if (audioElement) {
        audioElement.pause();
        audioElement.src = '';
        audioElement.load();
        audioElement = null;
    }
    currentSong = null;
    dotNetHelper = null;
}

function notifyStatusChanged() {
    if (dotNetHelper) {
        try {
            const status = getStatus();
            dotNetHelper.invokeMethodAsync('OnStatusChanged', status)
                .catch(error => {
                    console.error('Error calling OnStatusChanged:', error);
                });
        } catch (error) {
            console.error('Error getting status or calling OnStatusChanged:', error);
        }
    }
}

// Enable audio context on user interaction (required by some browsers)
document.addEventListener('click', enableAudioContext, { once: true });
document.addEventListener('keydown', enableAudioContext, { once: true });

function enableAudioContext() {
    if (window.AudioContext || window.webkitAudioContext) {
        const AudioContextClass = window.AudioContext || window.webkitAudioContext;
        const audioContext = new AudioContextClass();
        
        if (audioContext.state === 'suspended') {
            audioContext.resume().then(() => {
                console.log('AudioContext resumed');
            });
        }
    }
}
