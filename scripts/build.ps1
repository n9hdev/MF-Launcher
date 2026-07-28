param(
    [switch]$Release,
    [switch]$Service,
    [switch]$Frontend
)

$Root = Split-Path -Parent $PSScriptRoot

if ($Frontend) {
    Write-Host "Building frontend..." -ForegroundColor Cyan
    Set-Location "$Root\src\frontend"
    npm install
    if ($Release) {
        npm run build
    } else {
        npx vite build
    }
    Write-Host "Frontend build complete" -ForegroundColor Green
}

if ($Service -or (!$Frontend)) {
    Write-Host "Building .NET backend..." -ForegroundColor Cyan
    $config = if ($Release) { "Release" } else { "Debug" }
    dotnet restore "$Root\MafiaCityAntiCheat.sln"
    dotnet build "$Root\MafiaCityAntiCheat.sln" -c $config
    if ($LASTEXITCODE -eq 0) {
        Write-Host ".NET build complete ($config)" -ForegroundColor Green
    } else {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
}
