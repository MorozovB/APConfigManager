const { app, BrowserWindow, ipcMain, Notification, Menu } = require('electron');
const { spawn } = require('child_process');
const http = require('http');
const path = require('path');
const fs = require('fs');

app.setName('AP Configuration Manager');
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
  if (mainWindow.isFocused() && !mainWindow.isMinimized()) return;

  // flashFrame maps to the X11 urgency hint / Windows taskbar; on Wayland it is a
  // no-op, so a desktop notification is the reliable attention cue on Linux (KDE
  // shows it and plays its notification sound).
  mainWindow.flashFrame(true);

    if (Notification.isSupported()) {
    const iconPath = path.join(__dirname, 'build', 'icon.png');
    const notification = new Notification({
      title: 'AP Configuration Manager',
      body: 'Operations finished.',
      ...(fs.existsSync(iconPath) ? { icon: iconPath } : {}),
    });
    // Clicking the notification brings the window back to the front.
    notification.on('click', () => {
      if (!mainWindow) return;
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.show();
      mainWindow.focus();
    });
    notification.show();
  } else {
    console.warn('[shell] Desktop notifications are not supported on this system.');
  }
});

app.on('browser-window-focus', () => {
  if (mainWindow) mainWindow.flashFrame(false);
});

// ---------------------------------------------------------------------------
// Device-operation proxy
// ---------------------------------------------------------------------------
// Long device operations (flash / params / bootloader) go through the main
// process instead of the renderer: Chromium caps concurrent HTTP connections at
// 6 per host, which stalls the 7th session during a group run. Node's socket
// pool (http.globalAgent, maxSockets = Infinity) has no such cap, so all run
// concurrently. Progress/completion still arrive over SignalR in the renderer.
ipcMain.handle('api:operation', async (_event, payload) => {
  const { path: apiPath, fileName, fileBuffer } = payload || {};
  const target = new URL(API_URL + apiPath);

  const headers = {};
  let body = null;
  if (fileBuffer) {
    const boundary = '----apcm-' + Date.now().toString(16) + '-' + Math.random().toString(16).slice(2);
    const head = Buffer.from(
      `--${boundary}\r\n` +
      `Content-Disposition: form-data; name="file"; filename="${(fileName || 'file').replace(/"/g, '')}"\r\n` +
      `Content-Type: application/octet-stream\r\n\r\n`,
      'utf8',
    );
    const tail = Buffer.from(`\r\n--${boundary}--\r\n`, 'utf8');
    body = Buffer.concat([head, Buffer.from(fileBuffer), tail]);
    headers['Content-Type'] = `multipart/form-data; boundary=${boundary}`;
    headers['Content-Length'] = body.length;
  }

  return await new Promise((resolve, reject) => {
    const req = http.request(
      {
        hostname: target.hostname,
        port: target.port,
        path: target.pathname + target.search,
        method: 'POST',
        headers,
        timeout: 600000,
      },
      (res) => {
        let data = '';
        res.setEncoding('utf8');
        res.on('data', (chunk) => { data += chunk; });
        res.on('end', () => resolve({ status: res.statusCode, body: data }));
      },
    );
    req.on('timeout', () => req.destroy(new Error('Operation timed out')));
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
});

// ---------------------------------------------------------------------------
// Custom window controls (frameless window)
// ---------------------------------------------------------------------------
ipcMain.on('window:minimize', () => mainWindow?.minimize());
ipcMain.on('window:toggle-maximize', () => {
  if (!mainWindow) return;
  if (mainWindow.isMaximized()) mainWindow.unmaximize();
  else mainWindow.maximize();
});
ipcMain.on('window:close', () => mainWindow?.close());
ipcMain.handle('window:is-maximized', () => mainWindow?.isMaximized() ?? false);

// ---------------------------------------------------------------------------
// Window
// ---------------------------------------------------------------------------
async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    title: 'AP Configuration Manager',
    backgroundColor: '#1e1e24',
    show: false,
    frame: false,
        webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, 'preload.js'),
      // Keep the renderer's timers/JS (completion detection) alive when the window
      // is minimized — Chromium throttles background windows by default.
      backgroundThrottling: false,
    },
  });

  mainWindow.once('ready-to-show', () => mainWindow.show());
  mainWindow.on('closed', () => { mainWindow = null; });

   // Keep the renderer's maximize/restore icon in sync with the actual state.
  const sendMaximized = () => {
    if (mainWindow) mainWindow.webContents.send('window:maximized-changed', mainWindow.isMaximized());
  };
  mainWindow.on('maximize', sendMaximized);
  mainWindow.on('unmaximize', sendMaximized);

  await mainWindow.loadURL(await resolveUrl());
}

// ---------------------------------------------------------------------------
// App lifecycle
// ---------------------------------------------------------------------------
app.whenReady().then(async () => {

  Menu.setApplicationMenu(null);
  // If the API port is already served (e.g. a detached instance from a previous
  // run that outlived its window), reuse it instead of spawning a second one.
  if (await ping(API_URL)) {
    console.warn('[shell] API port already in use — reusing the existing instance.');
  } else {
    startApi();
  }

  const ok = await waitForApi();
  if (!ok) {
    const reason = describeApiFailure();
    console.error('[shell] API did not become ready:', reason);
    await showApiError(reason);
    return;
  }

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