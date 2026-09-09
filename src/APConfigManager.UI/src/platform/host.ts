export function notifyOperationsFinished(): void {
    window.chrome?.webview?.postMessage({ type: 'operations-finished' });    // (Electron): window.electronAPI?.notifyOperationsFinished();
}
declare global {
    interface Window {
        electronAPI?: {
            notifyOperationsFinished: () => void;
        };
    }
}
export {};