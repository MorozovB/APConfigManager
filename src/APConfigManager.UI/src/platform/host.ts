let audioCtx: AudioContext | null = null;

/**
 * Short two-note chime for background completion. Silent while the window is
 * focused — the user is already looking, so only the taskbar flash is needed
 * there. Any audio failure is swallowed: the native flash still notifies.
 */
function playCompletionChime(): void {
    if (document.hasFocus()) {
        return;
    }

    try {
        audioCtx ??= new AudioContext();
        if (audioCtx.state === 'suspended') {
            void audioCtx.resume();
        }

        const now = audioCtx.currentTime;
        const notes = [660, 880]; // E5, A5

        for (let i = 0; i < notes.length; i++) {
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();
            const start = now + i * 0.18;

            osc.type = 'sine';
            osc.frequency.value = notes[i];

            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(0.2, start + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.22);

            osc.connect(gain).connect(audioCtx.destination);
            osc.start(start);
            osc.stop(start + 0.24);
        }
    } catch {
        /* audio unavailable — the taskbar flash still notifies */
    }
}

/**
 * Notifies the user that a batch of operations finished while the app was in
 * the background: a completion chime (renderer) plus a taskbar flash (main
 * process, via the Electron preload bridge). Both stay quiet when focused.
 */
export function notifyOperationsFinished(): void {
    playCompletionChime();
    window.electronAPI?.notifyOperationsFinished();
}

declare global {
    interface Window {
        electronAPI?: {
            notifyOperationsFinished: () => void;
        };
    }
}

export {};