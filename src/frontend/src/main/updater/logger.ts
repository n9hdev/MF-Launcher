import * as fs from 'fs';
import * as path from 'path';

const LOG_DIR = process.env.PROGRAMDATA
  ? path.join(process.env.PROGRAMDATA, 'AntiCheat')
  : path.join(process.env.APPDATA || 'C:\\ProgramData', 'AntiCheat');

const LOG_FILE = path.join(LOG_DIR, 'Updater.log');
const MAX_SIZE = 5 * 1024 * 1024;

function ensureDir(): void {
  try {
    if (!fs.existsSync(LOG_DIR)) fs.mkdirSync(LOG_DIR, { recursive: true });
  } catch { /* best effort */ }
}

function rotateIfNeeded(): void {
  try {
    if (fs.existsSync(LOG_FILE) && fs.statSync(LOG_FILE).size > MAX_SIZE) {
      const rotated = LOG_FILE.replace('.log', '.old.log');
      if (fs.existsSync(rotated)) fs.unlinkSync(rotated);
      fs.renameSync(LOG_FILE, rotated);
    }
  } catch { /* best effort */ }
}

export function writeUpdaterLog(level: string, message: string, data?: Record<string, unknown>): void {
  try {
    ensureDir();
    rotateIfNeeded();
    const timestamp = new Date().toISOString();
    const dataStr = data ? ` ${JSON.stringify(data)}` : '';
    const line = `[${timestamp}] [${level.toUpperCase()}] ${message}${dataStr}\n`;
    fs.appendFileSync(LOG_FILE, line, 'utf-8');
  } catch { /* best effort */ }
}

export const updaterLog = {
  info: (msg: string, data?: Record<string, unknown>) => writeUpdaterLog('INFO', msg, data),
  warn: (msg: string, data?: Record<string, unknown>) => writeUpdaterLog('WARN', msg, data),
  error: (msg: string, data?: Record<string, unknown>) => writeUpdaterLog('ERROR', msg, data),
  success: (msg: string, data?: Record<string, unknown>) => writeUpdaterLog('SUCCESS', msg, data),
};
