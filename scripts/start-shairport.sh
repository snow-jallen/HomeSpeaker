#!/bin/sh
# Startup script for shairport-sync that mirrors the AudioDeviceDetector.cs priority:
#   1. ALSA_CARD environment variable override
#   2. USB audio devices (external DACs or speakers)
#   3. "Headphones" (Raspberry Pi headphone jack fallback)
#   4. First available device
#   5. PulseAudio fallback if no ALSA device found

CARD=""

if [ -n "$ALSA_CARD" ]; then
    echo "start-shairport: Using ALSA_CARD override: $ALSA_CARD"
    CARD="$ALSA_CARD"
else
    # Prefer USB audio devices (UAC prefix or USB in name/description)
    CARD=$(aplay -l 2>/dev/null | grep 'card [0-9]\+:' | grep -i 'USB\|UAC' | head -1 | sed 's/card [0-9]\+: \([^ ]*\) .*/\1/')

    if [ -z "$CARD" ]; then
        # Fall back to Headphones (Raspberry Pi headphone jack)
        CARD=$(aplay -l 2>/dev/null | grep 'card [0-9]\+:' | grep 'Headphones' | head -1 | sed 's/card [0-9]\+: \([^ ]*\) .*/\1/')
    fi

    if [ -z "$CARD" ]; then
        # Fall back to first available device
        CARD=$(aplay -l 2>/dev/null | grep 'card [0-9]\+:' | head -1 | sed 's/card [0-9]\+: \([^ ]*\) .*/\1/')
    fi
fi

if [ -n "$CARD" ]; then
    echo "start-shairport: Using ALSA card: $CARD"

    # Pick a hardware mixer control with the same priority AudioDeviceDetector.cs
    # uses, so AirPlay volume moves the same ALSA control as the HomeSpeaker app
    # and the physical knob instead of applying invisible software attenuation.
    MIXER=""
    for CANDIDATE in PCM Master Speaker Headphone Digital; do
        if amixer -c "$CARD" sget "$CANDIDATE" >/dev/null 2>&1; then
            MIXER="$CANDIDATE"
            break
        fi
    done

    if [ -n "$MIXER" ]; then
        echo "start-shairport: Using mixer control: $MIXER"
        exec /init -a "$AIRPLAY_NAME" -o alsa -- -d hw:"$CARD" -c "$MIXER"
    fi

    echo "start-shairport: No known mixer control found; using software volume"
    exec /init -a "$AIRPLAY_NAME" -o alsa -- -d hw:"$CARD"
else
    echo "start-shairport: No ALSA card detected, falling back to PulseAudio"
    exec /init -a "$AIRPLAY_NAME" -o pa
fi
