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
Object.defineProperty(exports, "__esModule", { value: true });
exports.verifyManifestSignature = verifyManifestSignature;
exports.computeFileSha256 = computeFileSha256;
exports.verifyAuthenticode = verifyAuthenticode;
exports.isVersionNewer = isVersionNewer;
exports.isAboveMinVersion = isAboveMinVersion;
const crypto = __importStar(require("crypto"));
const fs = __importStar(require("fs"));
const child_process_1 = require("child_process");
const public_key_1 = require("./public-key");
function verifyManifestSignature(manifest) {
    try {
        const signature = manifest.signature;
        if (!signature)
            return false;
        const { signature: _, ...fieldsToVerify } = manifest;
        const canonicalJson = JSON.stringify(fieldsToVerify, Object.keys(fieldsToVerify).sort());
        const verifier = crypto.createVerify('sha256');
        verifier.update(canonicalJson, 'utf-8');
        verifier.end();
        return verifier.verify(public_key_1.UPDATE_PUBLIC_KEY, signature, 'base64');
    }
    catch {
        return false;
    }
}
function computeFileSha256(filePath) {
    return new Promise((resolve, reject) => {
        try {
            const hash = crypto.createHash('sha256');
            const readStream = fs.createReadStream(filePath);
            readStream.on('data', (chunk) => {
                hash.update(chunk instanceof Buffer ? chunk : Buffer.from(chunk));
            });
            readStream.on('end', () => resolve(hash.digest('hex').toUpperCase()));
            readStream.on('error', reject);
        }
        catch (err) {
            reject(err);
        }
    });
}
function verifyAuthenticode(filePath, expectedPublisher, expectedThumbprint) {
    return new Promise((resolve) => {
        const escapedPath = filePath.replace(/'/g, "''");
        const psScript = `
$sig = Get-AuthenticodeSignature -FilePath '${escapedPath}' -ErrorAction SilentlyContinue
if (-not $sig) {
  Write-Output "STATUS=NoSignature"
  exit
}
Write-Output "STATUS=$($sig.Status)"
Write-Output "PUBLISHER=$($sig.SignerCertificate.Subject)"
Write-Output "THUMBPRINT=$($sig.SignerCertificate.Thumbprint)"
Write-Output "IS_TIMESTAMPED=$($sig.IsOSBinary -or $sig.TimestampCertificate -ne $null)"
`;
        (0, child_process_1.exec)(`powershell -NoProfile -ExecutionPolicy Bypass -Command "${psScript.replace(/"/g, '\\"')}"`, {
            timeout: 10000,
        }, (error, stdout) => {
            try {
                const lines = stdout.split('\n').map(l => l.trim()).filter(Boolean);
                const get = (key) => {
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
            }
            catch (err) {
                resolve({ valid: false, error: `Verification error: ${err.message}` });
            }
        });
    });
}
function isVersionNewer(current, candidate) {
    const parse = (v) => v.split('.').map(Number);
    const a = parse(current);
    const b = parse(candidate);
    for (let i = 0; i < Math.max(a.length, b.length); i++) {
        const na = a[i] || 0;
        const nb = b[i] || 0;
        if (nb > na)
            return true;
        if (nb < na)
            return false;
    }
    return false;
}
function isAboveMinVersion(version, minVersion) {
    const parse = (v) => v.split('.').map(Number);
    const a = parse(version);
    const b = parse(minVersion);
    for (let i = 0; i < Math.max(a.length, b.length); i++) {
        const na = a[i] || 0;
        const nb = b[i] || 0;
        if (na > nb)
            return true;
        if (na < nb)
            return false;
    }
    return true;
}
