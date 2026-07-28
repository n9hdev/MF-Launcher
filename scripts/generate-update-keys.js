const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const keysDir = path.join(root, 'keys');
const frontendUpdaterDir = path.join(root, 'src', 'frontend', 'src', 'main', 'updater');

if (!fs.existsSync(keysDir)) fs.mkdirSync(keysDir, { recursive: true });
if (!fs.existsSync(frontendUpdaterDir)) fs.mkdirSync(frontendUpdaterDir, { recursive: true });

const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', {
  modulusLength: 4096,
  publicKeyEncoding: { type: 'spki', format: 'pem' },
  privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
});

fs.writeFileSync(path.join(keysDir, 'update-private.pem'), privateKey, 'utf-8');
fs.writeFileSync(path.join(keysDir, 'update-public.pem'), publicKey, 'utf-8');

const publicKeyTs = `export const UPDATE_PUBLIC_KEY = \`${publicKey.replace(/\n$/, '')}\n\`;\n`;
fs.writeFileSync(path.join(frontendUpdaterDir, 'public-key.ts'), publicKeyTs, 'utf-8');

console.log('=== RSA-4096 key pair generated ===');
console.log(`Private key: keys/update-private.pem  (DO NOT COMMIT)`);
console.log(`Public key:  keys/update-public.pem`);
console.log(`Embedded:    src/frontend/src/main/updater/public-key.ts`);
