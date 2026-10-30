// albumArtDrop.js - lets the album art review page take a cover from a file, a drag and drop, or the clipboard.
// Each group's card carries data-album-art-key; an image pasted or dropped anywhere in a card goes to that group,
// and a [data-album-art-choose] button opens a file picker for it. Whatever the browser can show (GIF, WebP, AVIF,
// BMP, PNG, JPEG...) is drawn onto a canvas and sent to the page as a JPEG no bigger than MAX_SIZE on its long side,
// so the server only ever sees JPEGs. An animated GIF gives its first frame.
window.albumArtDrop = (function () {
    const MAX_SIZE = 1200;
    const QUALITY = 0.9;
    let state = null; // { dotNetRef, input, pendingKey, handlers }

    function keyOf(element) {
        const card = element && element.closest ? element.closest('[data-album-art-key]') : null;
        return card ? card.getAttribute('data-album-art-key') : null;
    }

    function imageFileFrom(dataTransfer) {
        if (!dataTransfer) return null;
        for (const item of dataTransfer.items || []) {
            if (item.kind === 'file' && item.type.startsWith('image/')) return item.getAsFile();
        }
        for (const file of dataTransfer.files || []) {
            if (file.type.startsWith('image/')) return file;
        }
        return null;
    }

    async function decode(blob) {
        try {
            return await createImageBitmap(blob);
        } catch {
            // SVG and a few others only decode through an <img>.
            const url = URL.createObjectURL(blob);
            try {
                const img = new Image();
                img.src = url;
                await img.decode();
                return img;
            } finally {
                URL.revokeObjectURL(url);
            }
        }
    }

    async function toJpeg(blob) {
        const source = await decode(blob);
        const width = source.naturalWidth || source.width;
        const height = source.naturalHeight || source.height;
        if (!width || !height) throw new Error('That image has no size.');
        const scale = Math.min(1, MAX_SIZE / Math.max(width, height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(width * scale));
        canvas.height = Math.max(1, Math.round(height * scale));
        const context = canvas.getContext('2d');
        // JPEG has no transparency; fill so transparent GIF/PNG areas come out white, not black.
        context.fillStyle = '#ffffff';
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.drawImage(source, 0, 0, canvas.width, canvas.height);
        if (source.close) source.close();
        return await new Promise(function (resolve, reject) {
            canvas.toBlob(function (jpeg) { jpeg ? resolve(jpeg) : reject(new Error('Couldn\'t convert that image.')); }, 'image/jpeg', QUALITY);
        });
    }

    async function send(key, blob, name) {
        if (!state || !key || !blob) return;
        const dotNetRef = state.dotNetRef;
        try {
            await dotNetRef.invokeMethodAsync('ImageConvertingAsync', key);
            const jpeg = await toJpeg(blob);
            await dotNetRef.invokeMethodAsync('ReceiveImageAsync', key, DotNet.createJSStreamReference(jpeg), name || '');
        } catch (error) {
            const message = (error && error.message) || String(error);
            dotNetRef.invokeMethodAsync('ImageFailedAsync', key, 'Couldn\'t read that image: ' + message).catch(function () { /* circuit gone */ });
        }
    }

    function attach(dotNetRef) {
        detach();
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        input.hidden = true;
        document.body.appendChild(input);

        const handlers = {
            paste: function (event) {
                const key = keyOf(event.target) || keyOf(document.activeElement);
                const file = imageFileFrom(event.clipboardData);
                if (!key || !file) return; // plain text (a link) pastes into the box as usual
                event.preventDefault();
                send(key, file, file.name || 'pasted image');
            },
            dragover: function (event) {
                if (!keyOf(event.target)) return;
                event.preventDefault();
                event.dataTransfer.dropEffect = 'copy';
            },
            drop: function (event) {
                const key = keyOf(event.target);
                if (!key) return;
                event.preventDefault();
                const file = imageFileFrom(event.dataTransfer);
                if (file) send(key, file, file.name);
                else state.dotNetRef.invokeMethodAsync('ImageFailedAsync', key, 'That wasn\'t an image file.').catch(function () { });
            },
            click: function (event) {
                const button = event.target.closest && event.target.closest('[data-album-art-choose]');
                if (!button) return;
                state.pendingKey = keyOf(button);
                input.value = '';
                input.click();
            },
            change: function () {
                const file = input.files && input.files[0];
                if (file && state && state.pendingKey) send(state.pendingKey, file, file.name);
            },
        };

        document.addEventListener('paste', handlers.paste);
        document.addEventListener('dragover', handlers.dragover);
        document.addEventListener('drop', handlers.drop);
        document.addEventListener('click', handlers.click);
        input.addEventListener('change', handlers.change);
        state = { dotNetRef, input, pendingKey: null, handlers };
    }

    function detach() {
        if (!state) return;
        document.removeEventListener('paste', state.handlers.paste);
        document.removeEventListener('dragover', state.handlers.dragover);
        document.removeEventListener('drop', state.handlers.drop);
        document.removeEventListener('click', state.handlers.click);
        state.input.remove();
        state = null;
    }

    return { attach, detach };
})();
