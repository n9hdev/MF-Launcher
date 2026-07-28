import { BrowserWindow, app } from 'electron';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { spawn, exec } from 'child_process';
import http from 'http';
import https from 'https';
import { updaterLog } from './logger';
import {
  verifyManifestSignature,
  computeFileSha256,
  verifyAuthenticode,
  isVersionNewer,
  isAboveMinVersion,
  type SignedManifest,
} from './verifier';

const API_BASE = process.env.VITE_API_BASE_URL || 'http://25.20.173.193:5000';
const EXPECTED_PUBLISHER = 'MF CITY, Inc.';
const EXPECTED_THUMBPRINT = 'BD3E5B7130E53D5DCDEF89186F26091FCD10587A'; // test cert thumbprint

const MAX_RETRIES = 4;
const INITIAL_BACKOFF_MS = 1000;
const CONNECT_TIMEOUT_MS = 15000;
const DOWNLOAD_TIMEOUT_MS = 300000;

async function httpFetch(url: string, timeoutMs: number): Promise<string> {
  const mod = url.startsWith('https') ? https : http;
  return new Promise((resolve, reject) => {
    const req = mod.get(url, { timeout: timeoutMs }, (res) => {
      let data = '';
      res.on('data', (chunk) => { data += chunk; });
      res.on('end', () => {
        if (res.statusCode === 200) resolve(data);
        else reject(new Error(`HTTP ${res.statusCode}`));
      });
    });
    req.on('error', reject);
    req.on('timeout', () => { req.destroy(); reject(new Error('Connection timeout')); });
  });
}

async function fetchWithRetry(url: string, timeoutMs: number, label: string): Promise<string> {
  let lastError: Error | undefined;
  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    try {
      if (attempt > 0) {
        const delay = INITIAL_BACKOFF_MS * Math.pow(2, attempt - 1);
        updaterLog.info(`Retry ${attempt}/${MAX_RETRIES} for ${label}`, { delayMs: delay });
        await new Promise(r => setTimeout(r, delay));
      }
      return await httpFetch(url, timeoutMs);
    } catch (err: any) {
      lastError = err;
      updaterLog.warn(`Request failed for ${label}`, { attempt: attempt + 1, error: err.message });
    }
  }
  throw lastError || new Error(`Request failed after ${MAX_RETRIES + 1} attempts`);
}

export async function checkForUpdates(): Promise<{
  hasUpdate: boolean;
  currentVersion: string;
  latestVersion: string;
  downloadUrl: string;
  fallbackDownloadUrl: string;
  sha256: string;
  size: number;
  isCritical: boolean;
  changelog: string;
  error?: string;
}> {
  updaterLog.info('Update check started');

  try {
    const manifestUrl = `${API_BASE}/api/updates/latest`;
    const json = await fetchWithRetry(manifestUrl, CONNECT_TIMEOUT_MS, 'manifest download');
    const manifest: SignedManifest = JSON.parse(json);
    updaterLog.info('Manifest downloaded', { version: manifest.version });

    const sigValid = verifyManifestSignature(manifest);
    if (!sigValid) {
      updaterLog.error('Manifest signature verification FAILED');
      return {
        hasUpdate: false, currentVersion: app.getVersion(), latestVersion: '',
        downloadUrl: '', fallbackDownloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
        error: 'Update manifest signature verification failed',
      };
    }
    updaterLog.success('Manifest signature verified');

    const currentVersion = app.getVersion();

    if (!isAboveMinVersion(manifest.version, manifest.minSupportedVersion || '0.0.0')) {
      updaterLog.error('Update version below minimum supported', {
        version: manifest.version, minSupported: manifest.minSupportedVersion,
      });
      return {
        hasUpdate: false, currentVersion, latestVersion: manifest.version,
        downloadUrl: '', fallbackDownloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
        error: 'Update version is below minimum supported version',
      };
    }

    const isNewer = isVersionNewer(currentVersion, manifest.version);
    const result = {
      hasUpdate: isNewer,
      currentVersion,
      latestVersion: manifest.version,
      downloadUrl: manifest.downloadUrl || '',
      fallbackDownloadUrl: manifest.fallbackDownloadUrl || '',
      sha256: manifest.sha256 || '',
      size: manifest.size || 0,
      isCritical: manifest.isCritical || false,
      changelog: manifest.changelog || '',
    };

    if (isNewer) {
      updaterLog.info('Update available', { from: currentVersion, to: manifest.version, critical: manifest.isCritical });
    } else {
      updaterLog.info('Already up to date', { currentVersion });
    }

    return result;
  } catch (err: any) {
    updaterLog.error('Update check failed', { error: err.message });
    return {
      hasUpdate: false, currentVersion: app.getVersion(), latestVersion: '',
      downloadUrl: '', fallbackDownloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
      error: err.message,
    };
  }
}

export async function downloadAndVerifyUpdate(
  downloadUrl: string,
  fallbackDownloadUrl: string,
  expectedSha256: string,
  expectedSize: number,
  onProgress?: (percent: number) => void,
): Promise<{ success: true; installerPath: string } | { success: false; error: string }> {
  const urls = [downloadUrl, fallbackDownloadUrl].filter(Boolean);
  updaterLog.info('Download started', { primaryUrl: downloadUrl, fallbackUrl: fallbackDownloadUrl || 'none', totalSources: urls.length });

  const tempDir = app.getPath('temp');
  const installerName = 'MafiaCityAntiCheat-Update.exe';
  const destPath = path.join(tempDir, installerName);
  let lastError = '';

  for (let i = 0; i < urls.length; i++) {
    const url = urls[i];
    const label = i === 0 ? 'primary' : 'fallback';
    updaterLog.info(`Attempting ${label} download`, { url });

    try {
      await downloadFile(url, destPath, expectedSize, onProgress);
      updaterLog.success(`${label} download completed`, { path: destPath });

      if (expectedSha256) {
        updaterLog.info('Verifying SHA-256 hash...');
        const actualHash = await computeFileSha256(destPath);
        if (actualHash !== expectedSha256.toUpperCase()) {
          cleanup(destPath);
          lastError = `SHA-256 mismatch: expected ${expectedSha256}, got ${actualHash}`;
          updaterLog.error(`${label} SHA-256 mismatch`, { expected: expectedSha256, actual: actualHash });
          if (i < urls.length - 1) {
            updaterLog.info('Trying next download source...');
            continue;
          }
          return { success: false, error: lastError };
        }
        updaterLog.success('SHA-256 verification passed');
      }

      updaterLog.info('Verifying Authenticode signature...');
      const authResult = await verifyAuthenticode(destPath, EXPECTED_PUBLISHER, EXPECTED_THUMBPRINT || undefined);
      if (!authResult.valid) {
        cleanup(destPath);
        lastError = `Authenticode verification failed: ${authResult.error}`;
        updaterLog.error(`${label} Authenticode verification failed`, { error: authResult.error, publisher: authResult.publisher });
        if (i < urls.length - 1) {
          updaterLog.info('Trying next download source...');
          continue;
        }
        return { success: false, error: lastError };
      }
      updaterLog.success('Authenticode verified', { publisher: authResult.publisher, thumbprint: authResult.thumbprint, source: label });

      return { success: true, installerPath: destPath };
    } catch (err: any) {
      cleanup(destPath);
      lastError = err.message;
      updaterLog.error(`${label} download failed`, { error: err.message });
      if (i < urls.length - 1) {
        updaterLog.info('Trying next download source...');
      }
    }
  }

  updaterLog.error('All download sources failed', { lastError });
  return { success: false, error: lastError || 'All download sources failed' };
}

export function installUpdate(installerPath: string): void {
  updaterLog.info('Starting installer', { path: installerPath });

  const appExe = app.getPath('exe');
  const currentPid = process.pid;
  const psPath = path.join(os.tmpdir(), `update-${Date.now()}.ps1`);
  const batPath = path.join(os.tmpdir(), `update-${Date.now()}.bat`);

  const psScript = `Write-Host "[1/5] Waiting for app to exit..."
$parent = Get-Process -Id ${currentPid} -ErrorAction SilentlyContinue
if ($parent) { $null = $parent.WaitForExit(60000) }
Write-Host "[2/5] App exited. Starting installer elevated..."
Start-Process -FilePath '${installerPath.replace(/'/g, "''")}' -ArgumentList '/SILENT /NORESTART' -Verb RunAs -Wait
Write-Host "[3/5] Installer completed. Launching app..."
Start-Process -FilePath '${appExe.replace(/'/g, "''")}'
Write-Host "[4/5] Cleaning up..."
Remove-Item -Path '${psPath.replace(/'/g, "''")}' -Force
Write-Host "[5/5] Done. This window will close automatically."
`;

  const batScript = `@echo off
title Updating Mafia City Anti-Cheat V6...
echo Updating Mafia City Anti-Cheat V6...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "${psPath}"
del "%~f0"
`;

  try {
    fs.writeFileSync(psPath, psScript, 'utf-8');
    fs.writeFileSync(batPath, batScript, 'utf-8');
  } catch (err: any) {
    updaterLog.error('Failed to write update scripts', { error: err.message });
    return;
  }

  spawn('cmd.exe', ['/c', 'start', '""', batPath], {
    detached: true, stdio: 'ignore'
  }).unref();

  updaterLog.info('Quitting app to allow installer to replace files');
  app.quit();
}

const MAX_REDIRECTS = 5;

function downloadFile(
  url: string, dest: string, expectedSize: number,
  onProgress?: (percent: number) => void,
  redirectCount = 0,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const mod = url.startsWith('https') ? https : http;
    const fileStream = fs.createWriteStream(dest);

    const req = mod.get(url, { timeout: DOWNLOAD_TIMEOUT_MS }, (res) => {
      if (res.statusCode && res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        fileStream.close();
        fs.unlink(dest, () => {});
        if (redirectCount >= MAX_REDIRECTS) {
          reject(new Error(`Too many redirects (${MAX_REDIRECTS})`));
          return;
        }
        const nextUrl = res.headers.location;
        updaterLog.info(`Following redirect ${res.statusCode}`, { from: url, to: nextUrl });
        downloadFile(nextUrl, dest, expectedSize, onProgress, redirectCount + 1).then(resolve).catch(reject);
        return;
      }

      if (res.statusCode !== 200) {
        fileStream.close();
        fs.unlink(dest, () => {});
        reject(new Error(`HTTP ${res.statusCode} for ${url}`));
        req.destroy();
        return;
      }

      const total = parseInt(res.headers['content-length'] || '0', 10);

      if (expectedSize > 0 && total > 0 && total !== expectedSize) {
        fileStream.close();
        fs.unlink(dest, () => {});
        reject(new Error(`Size mismatch: expected ${expectedSize} bytes, got ${total} bytes`));
        req.destroy();
        return;
      }

      let downloaded = 0;
      res.on('data', (chunk: Buffer) => {
        downloaded += chunk.length;
        fileStream.write(chunk);
        const win = BrowserWindow.getFocusedWindow();
        if (win && total > 0) {
          const pct = Math.round((downloaded / total) * 100);
          win.webContents.send('update:download-progress', { percent: pct, downloaded, total });
          onProgress?.(pct);
        }
      });
      res.on('end', () => {
        fileStream.end();
        if (downloaded === 0 && expectedSize > 0) {
          fs.unlink(dest, () => {});
          reject(new Error(`Empty response from ${url} (expected ${expectedSize} bytes)`));
          return;
        }
        resolve();
      });
      res.on('error', (err) => {
        fileStream.close();
        reject(err);
      });
    });

    req.on('timeout', () => {
      req.destroy();
      fileStream.close();
      reject(new Error('Download timeout'));
    });
    req.on('error', (err) => {
      fileStream.close();
      reject(err);
    });
  });
}

function cleanup(filePath: string): void {
  try { if (fs.existsSync(filePath)) fs.unlinkSync(filePath); } catch { /* best effort */ }
}
