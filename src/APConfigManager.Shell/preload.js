const { contextBridge, ipcRenderer } = require('electron');

// Safe bridge exposed to the SPA as window.electronAPI (contextIsolation on).
contextBridge.exposeInMainWorld('electronAPI', {
  notifyOperationsFinished: () => ipcRenderer.send('operations-finished'),
});