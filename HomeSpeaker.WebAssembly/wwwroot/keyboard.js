// Keyboard shortcuts for HomeSpeaker
window.homeSpeakerKeyboard = {
    init: function (dotnetHelper) {
        console.log("Initializing keyboard shortcuts");
        
        document.addEventListener('keydown', function (event) {
            // Ignore if user is typing in an input field
            if (event.target.tagName === 'INPUT' || 
                event.target.tagName === 'TEXTAREA' || 
                event.target.isContentEditable) {
                return;
            }

            let handled = false;

            switch (event.key) {
                case ' ': // Space = Play/Pause toggle
                    event.preventDefault();
                    dotnetHelper.invokeMethodAsync('OnPlayPause');
                    handled = true;
                    break;
                    
                case 'ArrowRight': // Right arrow = Skip forward
                    event.preventDefault();
                    dotnetHelper.invokeMethodAsync('OnSkipForward');
                    handled = true;
                    break;
                    
                case 'ArrowLeft': // Left arrow = Previous (or restart)
                    event.preventDefault();
                    dotnetHelper.invokeMethodAsync('OnSkipBack');
                    handled = true;
                    break;
                    
                case 's':
                case 'S': // S = Stop
                    if (!event.ctrlKey && !event.altKey && !event.metaKey) {
                        event.preventDefault();
                        dotnetHelper.invokeMethodAsync('OnStop');
                        handled = true;
                    }
                    break;
                    
                case 'r':
                case 'R': // R = Toggle repeat
                    if (!event.ctrlKey && !event.altKey && !event.metaKey) {
                        event.preventDefault();
                        dotnetHelper.invokeMethodAsync('OnToggleRepeat');
                        handled = true;
                    }
                    break;
                    
                case 'ArrowUp': // Up arrow = Volume up
                    event.preventDefault();
                    dotnetHelper.invokeMethodAsync('OnVolumeUp');
                    handled = true;
                    break;
                    
                case 'ArrowDown': // Down arrow = Volume down
                    event.preventDefault();
                    dotnetHelper.invokeMethodAsync('OnVolumeDown');
                    handled = true;
                    break;
            }

            if (handled) {
                console.log("Handled keyboard shortcut:", event.key);
            }
        });
    }
};
