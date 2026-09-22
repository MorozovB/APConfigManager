const { app, BrowserWindow, ipcMain } = require('electron');
const { spawn } = require('child_process');
const http = require('http');
const path = require('path');

const VITE_URL = 'http://localhost:5173';
const API_URL  = 'http://localhost:5000';

let mainWindow = null;
let apiProcess = null;
let lastApiFailure = null;

// ---------------------------------------------------------------------------
// API process lifecycle
// ---------------------------------------------------------------------------
function startApi() {
  const isDev = !app.isPackaged;
  let command, args, cwd;

  if (isDev) {
    cwd = path.resolve(__dirname, '..', 'APConfigManager.Api');
    command = 'dotnet';
    args = ['run', '--project', 'APConfigManager.Api.csproj', '--no-launch-profile'];
  } else {
    const exeName = process.platform === 'win32'
      ? 'APConfigManager.Api.exe'
      : 'APConfigManager.Api';
    cwd = path.join(process.resourcesPath, 'api');
    command = path.join(cwd, exeName);
    args = [];
  }

  apiProcess = spawn(command, args, {
    cwd,
    detached: true,
    stdio: 'ignore',
    env: {
      ...process.env,
      ASPNETCORE_URLS: API_URL,
      ASPNETCORE_ENVIRONMENT: isDev ? 'Development' : 'Production',
    },
  });

  apiProcess.on('error', (err) => {
      console.error('[shell] API spawn error:', err);
      lastApiFailure = `Failed to launch the backend process: ${err.message}`;
  });
  apiProcess.on('exit', (code, signal) => {
      if (code) {
          console.error('[shell] API exited. code=', code, 'signal=', signal);
          lastApiFailure =
              `The backend process exited unexpectedly (code ${code}` +
              (signal ? `, signal ${signal}` : '') + ').';
      }
      apiProcess = null;
  });
}

function stopApi() {
  if (!apiProcess) return;
  const pid = apiProcess.pid;
  apiProcess = null;
  try {
    if (process.platform === 'win32') {
      spawn('taskkill', ['/pid', String(pid), '/t', '/f']);
    } else {
      process.kill(-pid, 'SIGTERM');
    }
  } catch {
    /* already gone */
  }
}

// ---------------------------------------------------------------------------
// URL resolution
// ---------------------------------------------------------------------------
function ping(url) {
  return new Promise((resolve) => {
    const req = http.get(url, (res) => { res.destroy(); resolve(true); });
    req.on('error', () => resolve(false));
    req.setTimeout(1500, () => { req.destroy(); resolve(false); });
  });
}

async function waitForApi(timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await ping(API_URL)) return true;
    await new Promise((r) => setTimeout(r, 300));
  }
  return false;
}

async function resolveUrl() {
  if (await ping(VITE_URL)) return VITE_URL;
  return API_URL;
}

function describeApiFailure() {
  if (lastApiFailure) return lastApiFailure;
  return `The backend did not start responding on ${API_URL} within the timeout.`;
}

// Shows a clear failure page instead of loading a dead URL (blank/old UI).
async function showApiError(detail) {
  const win = new BrowserWindow({
    width: 760,
    height: 460,
    title: 'AP Configuration Manager',
    backgroundColor: '#1a1a2e',
    webPreferences: { contextIsolation: true, nodeIntegration: false },
  });
  mainWindow = win;
  win.on('closed', () => { if (mainWindow === win) mainWindow = null; });
  await win.loadFile(path.join(__dirname, 'error.html'), {
    search: `detail=${encodeURIComponent(detail)}`,
  });
}

// ---------------------------------------------------------------------------
// Block F: background-completion notification
// ---------------------------------------------------------------------------
ipcMain.on('operations-finished', () => {
  if (!mainWindow) return;
  if (mainWindow.isFocused()) return;
  mainWindow.flashFrame(true);
});

app.on('browser-window-focus', () => {
  if (mainWindow) mainWindow.flashFrame(false);
});

// ---------------------------------------------------------------------------
// Window
// ---------------------------------------------------------------------------
async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    title: 'AP Configuration Manager',
    backgroundColor: '#1a1a2e',
    show: false,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  mainWindow.once('ready-to-show', () => mainWindow.show());
  mainWindow.on('closed', () => { mainWindow = null; });

  await mainWindow.loadURL(await resolveUrl());
}

// ---------------------------------------------------------------------------
// App lifecycle
// ---------------------------------------------------------------------------
app.whenReady().then(async () => {
  startApi();
  const ok = await waitForApi();
  if (!ok) console.error('[shell] API did not become ready in time');
  await createWindow();
});

app.on('before-quit', stopApi);
app.on('will-quit', stopApi);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});