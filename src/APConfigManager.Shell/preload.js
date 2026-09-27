const { contextBridge, ipcRenderer } = require('electron');

// Safe bridge exposed to the SPA as window.electronAPI (contextIsolation on).
contextBridge.exposeInMainWorld('electronAPI', {
  notifyOperationsFinished: () => ipcRenderer.send('operations-finished'),
  window: {
    minimize: () => ipcRenderer.send('window:minimize'),
    toggleMaximize: () => ipcRenderer.send('window:toggle-maximize'),
    close: () => ipcRenderer.send('window:close'),
    isMaximized: () => ipcRenderer.invoke('window:is-maximized'),
    onMaximizeChange: (cb) => {
      const listener = (_e, isMax) => cb(isMax);
      ipcRenderer.on('window:maximized-changed', listener);
      return () => ipcRenderer.removeListener('window:maximized-changed', listener);
    },
  },
});