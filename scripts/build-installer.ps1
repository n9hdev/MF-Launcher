<#
    build-installer.ps1
    Production build script for Mafia City Anti-Cheat V6.

    Produces everything the Inno Setup installer needs:
      1. AntiCheat.Service   -> self-contained win-x64 publish (no .NET runtime needed on player PC)
      2. Dashboard (Electron) -> unpacked win-x64 build (electron-builder --dir)

    Output staging layout (under .\installer\staging):
      staging\service\   -> AntiCheat.Service.exe + deps + appsettings.json + Rules
      staging\dashboard\ -> Electron win-unpacked contents (Mafia City Anti-Cheat V6.exe + resources)

    After running this, compile installer\MafiaCityAntiCheat.iss with Inno Setup (ISCC.exe)
    to produce the final MafiaCityAntiCheat-Setup.exe.

    Usage:
      pwsh -File scripts\build-installer.ps1
#>

param(
    [string]$ServerIp = "25.20.173.193",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $root "installer\staging"
$serviceStage = Join-Path $staging "service"
$dashboardStage = Join-Path $staging "dashboard"

Write-Host "==> Cleaning staging directory" -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $serviceStage | Out-Null
New-Item -ItemType Directory -Force -Path $dashboardStage | Out-Null

# ----------------------------------------------------------------------------
# 1. Publish the background Windows Service (self-contained, single folder)
# ----------------------------------------------------------------------------
Write-Host "==> Publishing AntiCheat.Service (self-contained win-x64)" -ForegroundColor Cyan
$serviceProj = Join-Path $root "src\backend\AntiCheat.Service\AntiCheat.Service.csproj"
dotnet publish $serviceProj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $serviceStage
if ($LASTEXITCODE -ne 0) { throw "Service publish failed" }

Write-Host "    Service published to $serviceStage" -ForegroundColor Green

# ----------------------------------------------------------------------------
# 2. Build the Electron dashboard (unpacked dir) pointing at the server IP
# ----------------------------------------------------------------------------
Write-Host "==> Building Electron dashboard (VITE_API_BASE_URL=http://$ServerIp`:5000)" -ForegroundColor Cyan
$frontend = Join-Path $root "src\frontend"

# Ensure .env points at the production server
$envFile = Join-Path $frontend ".env"
Set-Content -Path $envFile -Value @(
    "VITE_API_BASE_URL=http://$ServerIp`:5000",
    "VITE_API_TIMEOUT=10000"
) -Encoding ASCII
Write-Host "    Wrote $envFile" -ForegroundColor Green

Push-Location $frontend
try {
    if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
        Write-Host "    Installing npm dependencies..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
    }

    Write-Host "    Compiling main/preload/renderer..." -ForegroundColor Yellow
    npm run copy:icon
    npm run build:main
    npm run build:preload
    npm run build:renderer
    if ($LASTEXITCODE -ne 0) { throw "renderer build failed" }

    Write-Host "    Packaging Electron app (unpacked dir)..." -ForegroundColor Yellow
    npx electron-builder --win --dir
    if ($LASTEXITCODE -ne 0) { throw "electron-builder failed" }
}
finally {
    Pop-Location
}

$unpacked = Join-Path $frontend "release\win-unpacked"
if (-not (Test-Path $unpacked)) { throw "Expected electron output not found at $unpacked" }
Copy-Item -Path (Join-Path $unpacked "*") -Destination $dashboardStage -Recurse -Force
Write-Host "    Dashboard staged to $dashboardStage" -ForegroundColor Green

# ----------------------------------------------------------------------------
# Done
# ----------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Staging complete." -ForegroundColor Cyan
Write-Host "    Service:   $serviceStage"
Write-Host "    Dashboard: $dashboardStage"
Write-Host ""
Write-Host "Next: compile the installer with Inno Setup:" -ForegroundColor Yellow
Write-Host "    ISCC.exe installer\MafiaCityAntiCheat.iss"
Write-Host ""
Write-Host "The final installer will be written to installer\output\MafiaCityAntiCheat-Setup.exe" -ForegroundColor Green

# ----------------------------------------------------------------------------
# 3. Prepare update artifacts — host the installer on the API server
# ----------------------------------------------------------------------------
$issPath = Join-Path $root "installer\MafiaCityAntiCheat.iss"
$versionLine = Select-String -Path $issPath -Pattern '#define MyAppVersion'
if ($versionLine -and $versionLine -match '"(.*?)"') {
    $appVersion = $Matches[1]
    Write-Host "==> Detected Inno Setup version: $appVersion" -ForegroundColor Cyan

    $outputDir = Join-Path $root "installer\output"
    $setupExe = Join-Path $outputDir "MafiaCityAntiCheat-Setup.exe"
    if (Test-Path $setupExe) {
        Write-Host "==> Computing SHA-256 of installer..." -ForegroundColor Cyan
        $sha256 = (Get-FileHash -Path $setupExe -Algorithm SHA256).Hash.ToUpper()
        Write-Host "    SHA-256: $sha256" -ForegroundColor Green
        $size = (Get-Item $setupExe).Length
        Write-Host "    Size: $size bytes" -ForegroundColor Green

        Write-Host "==> Copying installer to API updates folder..." -ForegroundColor Cyan
        $apiUpdatesDir = Join-Path $root "src\backend\AntiCheat.Api\updates"
        New-Item -ItemType Directory -Force -Path $apiUpdatesDir | Out-Null
        Copy-Item -Path $setupExe -Destination (Join-Path $apiUpdatesDir "MafiaCityAntiCheat-Setup.exe") -Force
        Write-Host "    Copied to $apiUpdatesDir" -ForegroundColor Green

        Write-Host "==> Signing update manifest..." -ForegroundColor Cyan
        $signScript = Join-Path $root "scripts\sign-update-manifest.js"
        $changelog = "- Fixed: False-positive detections`n- Fixed: Service crash on PCs without .NET 8`n- Fixed: Ban redirect not blocking login`n- Improved: Detection accuracy`n- New: Secure auto-update with signature verification`n- New: SHA-256 + Authenticode installer verification`n- Improved: Manifest signed with RSA-4096"
        $env:UPDATE_VERSION = $appVersion
        $env:UPDATE_SHA256 = $sha256
        $env:UPDATE_SIZE = $size.ToString()
        $env:UPDATE_CRITICAL = "false"
        $env:UPDATE_CHANGELOG = $changelog
        node $signScript
        if ($LASTEXITCODE -ne 0) { throw "Manifest signing failed" }
    } else {
        Write-Host "    WARNING: Installer not found at $setupExe - run ISCC.exe first" -ForegroundColor Yellow
    }
}
