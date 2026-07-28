"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.checkForUpdates = checkForUpdates;
exports.downloadAndVerifyUpdate = downloadAndVerifyUpdate;
exports.installUpdate = installUpdate;
const electron_1 = require("electron");
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
const child_process_1 = require("child_process");
const http_1 = __importDefault(require("http"));
const https_1 = __importDefault(require("https"));
const logger_1 = require("./logger");
const verifier_1 = require("./verifier");
const API_BASE = process.env.VITE_API_BASE_URL || 'http://25.20.173.193:5000';
const EXPECTED_PUBLISHER = 'MF CITY, Inc.';
const EXPECTED_THUMBPRINT = 'BD3E5B7130E53D5DCDEF89186F26091FCD10587A'; // test cert thumbprint
const MAX_RETRIES = 4;
const INITIAL_BACKOFF_MS = 1000;
const CONNECT_TIMEOUT_MS = 15000;
const DOWNLOAD_TIMEOUT_MS = 300000;
async function httpFetch(url, timeoutMs) {
    const mod = url.startsWith('https') ? https_1.default : http_1.default;
    return new Promise((resolve, reject) => {
        const req = mod.get(url, { timeout: timeoutMs }, (res) => {
            let data = '';
            res.on('data', (chunk) => { data += chunk; });
            res.on('end', () => {
                if (res.statusCode === 200)
                    resolve(data);
                else
                    reject(new Error(`HTTP ${res.statusCode}`));
            });
        });
        req.on('error', reject);
        req.on('timeout', () => { req.destroy(); reject(new Error('Connection timeout')); });
    });
}
async function fetchWithRetry(url, timeoutMs, label) {
    let lastError;
    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
        try {
            if (attempt > 0) {
                const delay = INITIAL_BACKOFF_MS * Math.pow(2, attempt - 1);
                logger_1.updaterLog.info(`Retry ${attempt}/${MAX_RETRIES} for ${label}`, { delayMs: delay });
                await new Promise(r => setTimeout(r, delay));
            }
            return await httpFetch(url, timeoutMs);
        }
        catch (err) {
            lastError = err;
            logger_1.updaterLog.warn(`Request failed for ${label}`, { attempt: attempt + 1, error: err.message });
        }
    }
    throw lastError || new Error(`Request failed after ${MAX_RETRIES + 1} attempts`);
}
async function checkForUpdates() {
    logger_1.updaterLog.info('Update check started');
    try {
        const manifestUrl = `${API_BASE}/api/updates/latest`;
        const json = await fetchWithRetry(manifestUrl, CONNECT_TIMEOUT_MS, 'manifest download');
        const manifest = JSON.parse(json);
        logger_1.updaterLog.info('Manifest downloaded', { version: manifest.version });
        const sigValid = (0, verifier_1.verifyManifestSignature)(manifest);
        if (!sigValid) {
            logger_1.updaterLog.error('Manifest signature verification FAILED');
            return {
                hasUpdate: false, currentVersion: electron_1.app.getVersion(), latestVersion: '',
                downloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
                error: 'Update manifest signature verification failed',
            };
        }
        logger_1.updaterLog.success('Manifest signature verified');
        const currentVersion = electron_1.app.getVersion();
        if (!(0, verifier_1.isAboveMinVersion)(manifest.version, manifest.minSupportedVersion || '0.0.0')) {
            logger_1.updaterLog.error('Update version below minimum supported', {
                version: manifest.version, minSupported: manifest.minSupportedVersion,
            });
            return {
                hasUpdate: false, currentVersion, latestVersion: manifest.version,
                downloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
                error: 'Update version is below minimum supported version',
            };
        }
        const isNewer = (0, verifier_1.isVersionNewer)(currentVersion, manifest.version);
        const result = {
            hasUpdate: isNewer,
            currentVersion,
            latestVersion: manifest.version,
            downloadUrl: manifest.downloadUrl || '',
            sha256: manifest.sha256 || '',
            size: manifest.size || 0,
            isCritical: manifest.isCritical || false,
            changelog: manifest.changelog || '',
        };
        if (isNewer) {
            logger_1.updaterLog.info('Update available', { from: currentVersion, to: manifest.version, critical: manifest.isCritical });
        }
        else {
            logger_1.updaterLog.info('Already up to date', { currentVersion });
        }
        return result;
    }
    catch (err) {
        logger_1.updaterLog.error('Update check failed', { error: err.message });
        return {
            hasUpdate: false, currentVersion: electron_1.app.getVersion(), latestVersion: '',
            downloadUrl: '', sha256: '', size: 0, isCritical: false, changelog: '',
            error: err.message,
        };
    }
}
async function downloadAndVerifyUpdate(downloadUrl, expectedSha256, expectedSize, onProgress) {
    logger_1.updaterLog.info('Download started', { url: downloadUrl });
    const tempDir = electron_1.app.getPath('temp');
    const installerName = 'MafiaCityAntiCheat-Update.exe';
    const destPath = path.join(tempDir, installerName);
    try {
        await downloadFile(downloadUrl, destPath, expectedSize, onProgress);
        logger_1.updaterLog.success('Download completed', { path: destPath });
        if (expectedSha256) {
            logger_1.updaterLog.info('Verifying SHA-256 hash...');
            const actualHash = await (0, verifier_1.computeFileSha256)(destPath);
            if (actualHash !== expectedSha256.toUpperCase()) {
                cleanup(destPath);
                logger_1.updaterLog.error('SHA-256 mismatch', { expected: expectedSha256, actual: actualHash });
                return { success: false, error: `SHA-256 mismatch: expected ${expectedSha256}, got ${actualHash}` };
            }
            logger_1.updaterLog.success('SHA-256 verification passed');
        }
        logger_1.updaterLog.info('Verifying Authenticode signature...');
        const authResult = await (0, verifier_1.verifyAuthenticode)(destPath, EXPECTED_PUBLISHER, EXPECTED_THUMBPRINT || undefined);
        if (!authResult.valid) {
            cleanup(destPath);
            logger_1.updaterLog.error('Authenticode verification failed', { error: authResult.error, publisher: authResult.publisher });
            return { success: false, error: `Authenticode verification failed: ${authResult.error}` };
        }
        logger_1.updaterLog.success('Authenticode verified', { publisher: authResult.publisher, thumbprint: authResult.thumbprint });
        return { success: true, installerPath: destPath };
    }
    catch (err) {
        cleanup(destPath);
        logger_1.updaterLog.error('Download/verification failed', { error: err.message });
        return { success: false, error: err.message };
    }
}
function installUpdate(installerPath) {
    logger_1.updaterLog.info('Starting installer', { path: installerPath });
    const installerProcess = (0, child_process_1.spawn)(installerPath, ['/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'], {
        detached: true,
        stdio: 'ignore',
    });
    installerProcess.unref();
    logger_1.updaterLog.info('Installer launched, quitting app');
    setTimeout(() => { electron_1.app.quit(); }, 1000);
}
function downloadFile(url, dest, expectedSize, onProgress) {
    return new Promise((resolve, reject) => {
        const mod = url.startsWith('https') ? https_1.default : http_1.default;
        const fileStream = fs.createWriteStream(dest);
        const req = mod.get(url, { timeout: DOWNLOAD_TIMEOUT_MS }, (res) => {
            const total = parseInt(res.headers['content-length'] || '0', 10);
            if (expectedSize > 0 && total > 0 && total !== expectedSize) {
                fileStream.close();
                fs.unlink(dest, () => { });
                reject(new Error(`Size mismatch: expected ${expectedSize} bytes, got ${total} bytes`));
                req.destroy();
                return;
            }
            let downloaded = 0;
            res.on('data', (chunk) => {
                downloaded += chunk.length;
                fileStream.write(chunk);
                const win = electron_1.BrowserWindow.getFocusedWindow();
                if (win && total > 0) {
                    const pct = Math.round((downloaded / total) * 100);
                    win.webContents.send('update:download-progress', { percent: pct, downloaded, total });
                    onProgress?.(pct);
                }
            });
            res.on('end', () => {
                fileStream.end();
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
function cleanup(filePath) {
    try {
        if (fs.existsSync(filePath))
            fs.unlinkSync(filePath);
    }
    catch { /* best effort */ }
}
