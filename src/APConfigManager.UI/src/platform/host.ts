export function notifyOperationsFinished(): void {
    window.chrome?.webview?.postMessage({ type: 'operations-finished' });
    // (Electron): window.electronAPI?.notifyOperationsFinished();
}