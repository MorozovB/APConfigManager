const { app, BrowserWindow } = require('electron');
const http = require('http');

const VITE_URL = 'http://localhost:5173';
const API_URL  = 'http://localhost:5000';

let mainWindow = null;

function ping(url) {
  return new Promise((resolve) => {
    const req = http.get(url, (res) => { res.destroy(); resolve(true); });
    req.on('error', () => resolve(false));
    req.setTimeout(1500, () => { req.destroy(); resolve(false); });
  });
}

async function resolveUrl() {
  // Dev with Vite (hot reload) wins; otherwise the API-served SPA.
  if (await ping(VITE_URL)) return VITE_URL;
  // API may still be starting — wait up to ~30s.
  for (let i = 0; i < 60; i++) {
    if (await ping(API_URL)) return API_URL;
    await new Promise(r => setTimeout(r, 500));
  }
  return API_URL; // last resort; window will show a connection error
}

async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    title: 'AP Configuration Manager',
    backgroundColor: '#1a1a2e',
    show: false,
    webPreferences: { contextIsolation: true, nodeIntegration: false },
  });

  mainWindow.once('ready-to-show', () => mainWindow.show());
  mainWindow.on('closed', () => { mainWindow = null; });

  await mainWindow.loadURL(await resolveUrl());
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });