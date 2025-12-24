@echo off
REM MF Update Service - Batch script version with progress bar
REM Arguments: %1=MainAppPath %2=DllPath %3=DllDownloadUrl %4=DllHash

setlocal enabledelayedexpansion

if "%~1"=="" (
    echo [ERROR] Invalid arguments
    exit /b 1
)

set "MAIN_APP=%~1"
set "DLL_PATH=%~2"
set "DLL_URL=%~3"
set "DLL_HASH=%~4"
set "TEMP_DLL=%~2.tmp"
set "DLL_DIR=%~dp2"

REM Change to DLL directory
cd /d "!DLL_DIR!"

echo.
echo [UPDATE] ============================================
echo [UPDATE] MF City Launcher - Update Service
echo [UPDATE] ============================================
echo.

REM Step 1: Close main application
echo [UPDATE] Step 1/4: Closing main application...
taskkill /F /IM MF_CITY_AntiCheat.exe 2>nul
taskkill /F /IM MF_CITY_AntiCheat.dll 2>nul
timeout /t 2 /nobreak

echo [UPDATE] [###########---------] 25%% Complete
echo.

REM Step 2: Download DLL with progress
echo [UPDATE] Step 2/4: Downloading DLL update...
echo [UPDATE] Connecting to server...
timeout /t 1 /nobreak

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; $ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest -Uri '!DLL_URL!' -OutFile '!TEMP_DLL!' -ErrorAction Stop } catch { exit 1 }" || (
    echo [ERROR] Download failed
    exit /b 1
)

echo [UPDATE] Download complete
echo [UPDATE] [###################-----] 50%% Complete
echo.

REM Step 3: Verify hash
echo [UPDATE] Step 3/4: Verifying update integrity...
for /f %%A in ('powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-FileHash '!TEMP_DLL!' -Algorithm SHA256).Hash"') do set "ACTUAL_HASH=%%A"

if /i "!ACTUAL_HASH!" neq "!DLL_HASH!" (
    echo [ERROR] Hash mismatch!
    echo [ERROR] Expected: !DLL_HASH!
    echo [ERROR] Got: !ACTUAL_HASH!
    del /f /q "!TEMP_DLL!"
    exit /b 1
)

echo [UPDATE] Hash verified successfully
echo [UPDATE] [#######################---] 75%% Complete
echo.

REM Step 4: Install update
echo [UPDATE] Step 4/4: Installing update...

REM Create backup
if exist "!DLL_PATH!" (
    if exist "!DLL_PATH!.backup" (
        del /f /q "!DLL_PATH!.backup"
    )
    ren "!DLL_PATH!" "MF_CITY_AntiCheat.dll.backup"
)

REM Install new DLL
ren "!TEMP_DLL!" "MF_CITY_AntiCheat.dll"

if not exist "!DLL_PATH!" (
    echo [ERROR] Installation failed
    exit /b 1
)

echo [UPDATE] DLL installed successfully
echo [UPDATE] [###########################] 100%% Complete
echo.

REM Delete old backup after successful install
timeout /t 1 /nobreak
if exist "!DLL_PATH!.backup" (
    del /f /q "!DLL_PATH!.backup"
    echo [UPDATE] Cleaned up old version
)

echo [UPDATE] Update completed successfully
echo [UPDATE] [###########################] 100%% Complete
echo.

REM Delete old backup after successful install
timeout /t 1 /nobreak
if exist "!DLL_PATH!.backup" (
    del /f /q "!DLL_PATH!.backup"
    echo [UPDATE] Cleaned up old version
)

REM Clear console and show completion message
cls
echo.
echo [UPDATE] ============================================
echo [UPDATE] Update Completed Successfully!
echo [UPDATE] ============================================
echo.
echo [UPDATE] The anti-cheat DLL has been updated.
echo [UPDATE] 
echo [UPDATE] Please restart the application to apply the update.
echo [UPDATE] 
echo [UPDATE] Press any key to continue...
echo.

REM Wait for user input
pause >nul

REM Exit successfully
exit /b 0


