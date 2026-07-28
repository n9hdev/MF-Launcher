const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const privateKeyPath = path.join(root, 'keys', 'update-private.pem');
const serviceAppSettings = path.join(root, 'src', 'backend', 'AntiCheat.Service', 'publish', 'appsettings.json');
const apiAppSettings = path.join(root, 'src', 'backend', 'AntiCheat.Api', 'appsettings.json');

const args = process.argv.slice(2);
const version = args[0] || process.env.UPDATE_VERSION;
const isCritical = args[1] === 'true' || process.env.UPDATE_CRITICAL === 'true';
const changelog = args[2] || process.env.UPDATE_CHANGELOG || '• New version released';

if (!version) {
  console.error('Usage: node sign-update-manifest.js <version> [isCritical] [changelog]');
  console.error('SHA-256 and size are auto-computed from the installer file.');
  process.exit(1);
}

const installerPath = path.resolve(root, 'installer', 'output', 'MafiaCityAntiCheat-Setup.exe');
if (!fs.existsSync(installerPath)) {
  console.error(`Installer not found at ${installerPath}`);
  console.error('Build the installer first: npm run build:installer');
  process.exit(1);
}

console.log(`Computing SHA-256 for ${installerPath}...`);
const installerData = fs.readFileSync(installerPath);
const sha256 = crypto.createHash('sha256').update(installerData).digest('hex').toUpperCase();
const size = installerData.length;
console.log(`  SHA-256: ${sha256}`);
console.log(`  Size: ${size} bytes`);

if (!fs.existsSync(privateKeyPath)) {
  console.error(`Private key not found at ${privateKeyPath}`);
  console.error('Run scripts/generate-update-keys.js first');
  process.exit(1);
}

const privateKey = fs.readFileSync(privateKeyPath, 'utf-8');

const releaseDate = new Date().toISOString().split('T')[0];
const downloadUrl = `http://25.20.173.193:5000/updates/MafiaCityAntiCheat-Setup.exe`;
const fallbackDownloadUrl = `https://github.com/n9hdev/MF-Launcher/releases/download/v${version}/MafiaCityAntiCheat-Setup.exe`;

const manifest = {
  version,
  releaseDate,
  downloadUrl,
  fallbackDownloadUrl,
  sha256: sha256.toUpperCase(),
  size: parseInt(size, 10),
  isCritical,
  minSupportedVersion: '6.0.0',
  changelog,
};

const sign = crypto.createSign('sha256');
const canonicalJson = JSON.stringify(manifest, Object.keys(manifest).sort());
sign.update(canonicalJson, 'utf-8');
sign.end();
const signature = sign.sign(privateKey, 'base64');

const signedManifest = { ...manifest, signature };
console.log('\n=== Signed Manifest ===');
console.log(JSON.stringify(signedManifest, null, 2));
console.log(`\nSignature length: ${signature.length} chars`);

function updateAppSettings(filePath) {
  if (!fs.existsSync(filePath)) {
    console.warn(`  WARNING: ${filePath} not found, skipping`);
    return;
  }
  try {
    let content = fs.readFileSync(filePath, 'utf-8');
    const json = JSON.parse(content);

    json.UpdateInfo = {
      LatestVersion: version,
      ReleaseDate: releaseDate,
      DownloadUrl: downloadUrl,
      FallbackDownloadUrl: fallbackDownloadUrl,
      Sha256: sha256.toUpperCase(),
      Size: parseInt(size, 10),
      IsCritical: isCritical,
      MinSupportedVersion: '6.0.0',
      Changelog: changelog,
      Signature: signature,
    };

    fs.writeFileSync(filePath, JSON.stringify(json, null, 2) + '\n', 'utf-8');
    console.log(`  Updated: ${filePath}`);
  } catch (err) {
    console.error(`  FAILED: ${filePath} - ${err.message}`);
  }
}

console.log('\n=== Updating appsettings.json files ===');
updateAppSettings(apiAppSettings);
