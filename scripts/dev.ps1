$Root = Split-Path -Parent $PSScriptRoot

Write-Host "Starting Mafia City Anti-Cheat V6 development environment..." -ForegroundColor Cyan

# Start .NET API
$apiJob = Start-Job -ScriptBlock {
    Set-Location $using:Root\src\backend\AntiCheat.Api
    dotnet run
}

# Start Vite dev server
$frontendJob = Start-Job -ScriptBlock {
    Set-Location $using:Root\src\frontend
    npm install
    npx vite
}

Write-Host "API starting on http://localhost:5000" -ForegroundColor Yellow
Write-Host "Frontend starting on http://localhost:5173" -ForegroundColor Yellow
Write-Host "Press any key to stop both services..." -ForegroundColor Cyan

$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Stop-Job $apiJob -ErrorAction SilentlyContinue
Stop-Job $frontendJob -ErrorAction SilentlyContinue
Remove-Job $apiJob -Force -ErrorAction SilentlyContinue
Remove-Job $frontendJob -Force -ErrorAction SilentlyContinue

Write-Host "Development environment stopped" -ForegroundColor Green
