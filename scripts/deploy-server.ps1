<#
    deploy-server.ps1
    Deploys AntiCheat.Api to the server machine (10.147.20.39).

    Usage:
      .\scripts\deploy-server.ps1               # publish + start
      .\scripts\deploy-server.ps1 -Action Stop   # stop the service
      .\scripts\deploy-server.ps1 -Action Restart
      .\scripts\deploy-server.ps1 -Action InstallService   # register as Windows Service
#>

param(
    [ValidateSet('Deploy', 'Stop', 'Start', 'Restart', 'InstallService')]
    [string]$Action = 'Deploy',
    [string]$Configuration = 'Release',
    [string]$TargetDir = 'C:\AntiCheat\Api'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$apiProj = Join-Path $root 'src\backend\AntiCheat.Api\AntiCheat.Api.csproj'
$serviceName = 'MafiaCityAntiCheatApi'

# Resolve ports from appsettings
$appSettings = Join-Path $root 'src\backend\AntiCheat.Api\appsettings.json'
$urls = 'http://0.0.0.0:5000'

function Publish-Api {
    Write-Host "==> Publishing AntiCheat.Api ($Configuration)" -ForegroundColor Cyan
    $publishDir = Join-Path $root 'publish'
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    dotnet publish $apiProj -c $Configuration -o $publishDir --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Write-Host "    Published to $publishDir" -ForegroundColor Green
}

function Copy-ToTarget {
    param([string]$Source)
    Write-Host "==> Copying to $TargetDir" -ForegroundColor Cyan
    if (Test-Path $TargetDir) { Remove-Item -Recurse -Force "$TargetDir\*" }
    else { New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null }
    Copy-Item -Path "$Source\*" -Destination $TargetDir -Recurse -Force
    Copy-Item -Path (Join-Path $root 'installer\README.md') -Destination $TargetDir -Force
    Write-Host "    Deployed to $TargetDir" -ForegroundColor Green
}

function Ensure-FirewallRule {
    Write-Host "==> Ensuring firewall rule for port 5000" -ForegroundColor Cyan
    $ruleName = 'AntiCheat API 5000'
    $existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
        Write-Host "    Firewall rule created" -ForegroundColor Green
    } else {
        Write-Host "    Firewall rule already exists" -ForegroundColor Yellow
    }
}

function Stop-Api {
    Write-Host "==> Stopping $serviceName" -ForegroundColor Cyan
    $svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        Write-Host "    Service stopped" -ForegroundColor Green
    } else {
        $proc = Get-Process -Name 'AntiCheat.Api' -ErrorAction SilentlyContinue
        if ($proc) { $proc | Stop-Process -Force; Write-Host "    Process stopped" -ForegroundColor Green }
        else { Write-Host "    Not running" -ForegroundColor Yellow }
    }
}

function Start-ApiService {
    Write-Host "==> Starting $serviceName" -ForegroundColor Cyan
    $svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($svc) {
        Start-Service -Name $serviceName
        Write-Host "    Service started" -ForegroundColor Green
    } else {
        Write-Host "    Service not installed, starting as process..." -ForegroundColor Yellow
        $exe = Join-Path $TargetDir 'AntiCheat.Api.exe'
        if (Test-Path $exe) {
            $args = @("--urls", $urls)
            Start-Process -FilePath $exe -ArgumentList $args -NoNewWindow
            Write-Host "    Process started: $exe" -ForegroundColor Green
        } else {
            throw "Not found: $exe. Run deploy first."
        }
    }
}

function Install-WindowsService {
    Write-Host "==> Installing Windows Service '$serviceName'" -ForegroundColor Cyan
    $exe = Join-Path $TargetDir 'AntiCheat.Api.exe'
    if (-not (Test-Path $exe)) { throw "Not found: $exe. Run deploy first." }

    $existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "    Service already exists, restarting..." -ForegroundColor Yellow
        Restart-Service -Name $serviceName -Force
        return
    }

    $binPath = "$exe --urls $urls"
    New-Service -Name $serviceName -BinaryPathName $binPath -DisplayName 'Mafia City Anti-Cheat API' -StartupType Automatic
    Start-Service -Name $serviceName
    Write-Host "    Service installed and started" -ForegroundColor Green
}

switch ($Action) {
    'Deploy' {
        Publish-Api
        Stop-Api
        Copy-ToTarget -Source (Join-Path $root 'publish')
        Ensure-FirewallRule
        Start-ApiService
        Write-Host "`n==> Deploy complete. API running at $urls" -ForegroundColor Green
    }
    'Stop' { Stop-Api }
    'Start' { Start-ApiService }
    'Restart' { Stop-Api; Start-ApiService }
    'InstallService' { Install-WindowsService }
}
