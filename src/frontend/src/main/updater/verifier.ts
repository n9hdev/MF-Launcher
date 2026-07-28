import * as crypto from 'crypto';
import * as fs from 'fs';
import * as path from 'path';
import { exec } from 'child_process';
import { UPDATE_PUBLIC_KEY } from './public-key';

export interface SignedManifest {
  version: string;
  releaseDate: string;
  downloadUrl: string;
  fallbackDownloadUrl: string;
  sha256: string;
  size: number;
  isCritical: boolean;
  minSupportedVersion: string;
  changelog: string;
  signature: string;
}

export interface AuthenticodeResult {
  valid: boolean;
  publisher?: string;
  thumbprint?: string;
  isTimestamped?: boolean;
  error?: string;
}

export function verifyManifestSignature(manifest: SignedManifest): boolean {
  try {
    const signature = manifest.signature;
    if (!signature) return false;

    const { signature: _, ...fieldsToVerify } = manifest;
    const canonicalJson = JSON.stringify(fieldsToVerify, Object.keys(fieldsToVerify).sort());
    const verifier = crypto.createVerify('sha256');
    verifier.update(canonicalJson, 'utf-8');
    verifier.end();
    return verifier.verify(UPDATE_PUBLIC_KEY, signature, 'base64');
  } catch {
    return false;
  }
}

export function computeFileSha256(filePath: string): Promise<string> {
  return new Promise((resolve, reject) => {
    try {
      const hash = crypto.createHash('sha256');
      const readStream = fs.createReadStream(filePath);
      readStream.on('data', (chunk: string | Buffer) => {
        hash.update(chunk instanceof Buffer ? chunk : Buffer.from(chunk));
      });
      readStream.on('end', () => resolve(hash.digest('hex').toUpperCase()));
      readStream.on('error', reject);
    } catch (err) {
      reject(err);
    }
  });
}

export function verifyAuthenticode(filePath: string, expectedPublisher?: string, expectedThumbprint?: string): Promise<AuthenticodeResult> {
  return new Promise((resolve) => {
    const psScript = `$sig = Get-AuthenticodeSignature -FilePath '${filePath.replace(/'/g, "''")}' -ErrorAction SilentlyContinue
if (-not $sig) { Write-Output 'STATUS=NoSignature'; exit }
Write-Output "STATUS=$($sig.Status)"
Write-Output "PUBLISHER=$($sig.SignerCertificate.Subject)"
Write-Output "THUMBPRINT=$($sig.SignerCertificate.Thumbprint)"
Write-Output "IS_TIMESTAMPED=$($sig.IsOSBinary -or $sig.TimestampCertificate -ne $null)"`;

    const psPath = path.join(require('os').tmpdir(), `auth-sig-${Date.now()}.ps1`);
    const fs = require('fs');
    try { fs.writeFileSync(psPath, psScript, 'utf-8'); } catch (e: any) {
      resolve({ valid: false, error: `Failed to write PS script: ${e.message}` });
      return;
    }

    exec(`powershell -NoProfile -ExecutionPolicy Bypass -File "${psPath}"`, { timeout: 10000 }, (error, stdout) => {
      try { if (fs.existsSync(psPath)) fs.unlinkSync(psPath); } catch { /* best effort */ }
      try {
        const lines = stdout.split('\n').map(l => l.trim()).filter(Boolean);
        const get = (key: string): string | undefined => {
          const line = lines.find(l => l.startsWith(`${key}=`));
          return line ? line.substring(key.length + 1) : undefined;
        };

        const status = get('STATUS');
        const publisher = get('PUBLISHER');
        const thumbprint = get('THUMBPRINT');
        const isTimestamped = get('IS_TIMESTAMPED') === 'True';

        if (status === 'NoSignature' || !status) {
          resolve({ valid: false, error: 'No Authenticode signature found' });
          return;
        }

        if (status !== 'Valid' && status !== 'UnknownError') {
          resolve({ valid: false, error: `Signature status: ${status}`, publisher, thumbprint });
          return;
        }

        if (expectedPublisher && publisher && !publisher.toLowerCase().includes(expectedPublisher.toLowerCase())) {
          resolve({ valid: false, error: `Publisher mismatch: expected "${expectedPublisher}", got "${publisher}"`, publisher, thumbprint });
          return;
        }

        if (expectedThumbprint && thumbprint && thumbprint.toUpperCase() !== expectedThumbprint.toUpperCase()) {
          resolve({ valid: false, error: `Thumbprint mismatch: expected ${expectedThumbprint}, got ${thumbprint}`, publisher, thumbprint });
          return;
        }

        resolve({ valid: true, publisher, thumbprint, isTimestamped });
      } catch (err: any) {
        resolve({ valid: false, error: `Verification error: ${err.message}` });
      }
    });
  });
}

export function isVersionNewer(current: string, candidate: string): boolean {
  const parse = (v: string): number[] => v.split('.').map(Number);
  const a = parse(current);
  const b = parse(candidate);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const na = a[i] || 0;
    const nb = b[i] || 0;
    if (nb > na) return true;
    if (nb < na) return false;
  }
  return false;
}

export function isAboveMinVersion(version: string, minVersion: string): boolean {
  const parse = (v: string): number[] => v.split('.').map(Number);
  const a = parse(version);
  const b = parse(minVersion);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const na = a[i] || 0;
    const nb = b[i] || 0;
    if (na > nb) return true;
    if (na < nb) return false;
  }
  return true;
}
