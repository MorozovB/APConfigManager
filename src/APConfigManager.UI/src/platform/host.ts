/**
 * Notifies the user that a batch of operations finished while the app was in the
 * background. The visible/audible cue is raised by the main process (taskbar
 * flash on X11/Windows, a desktop notification on Linux via the preload bridge).
 */
export function notifyOperationsFinished(): void {
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