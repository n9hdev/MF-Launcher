; ============================================================================
;  Mafia City Anti-Cheat V6 - Production Installer (Inno Setup)
;
;  Installs BOTH:
;    1. The Dashboard (Electron desktop app)
;    2. The Background Anti-Cheat Service (registered as a Windows Service)
;
;  Build prerequisites (run first):
;    pwsh -File scripts\build-installer.ps1
;  which stages files into installer\staging\dashboard and installer\staging\service.
;
;  Then compile this script with Inno Setup:
;    ISCC.exe installer\MafiaCityAntiCheat.iss
;
;  Output: installer\output\MafiaCityAntiCheat-Setup.exe
; ============================================================================

#define MyAppName "Mafia City Anti-Cheat V6"
#define MyAppVersion "6.3.6"
#define MyAppPublisher "MF CITY, Inc."
#define MyAppURL "https://discord.gg/HhEaPTWr3n"
#define MyDashboardExe "Mafia City Anti-Cheat V6.exe"
#define MyServiceExe "AntiCheat.Service.exe"
#define MyServiceName "MafiaCityAntiCheatV6"

[Setup]
AppId=com.mafia-city.anticheat.v6
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Mafia City Anti-Cheat V6
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\Dashboard\{#MyDashboardExe}
DisableProgramGroupPage=yes
; Service registration and Program Files write both require admin rights.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=MafiaCityAntiCheat-Setup
SetupIconFile=..\src\frontend\public\icon.ico
Compression=lzma2
CompressionThreads=auto
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#MyDashboardExe}
RestartApplications=no

[Code]
function InitializeSetup: Boolean;
var
  ResultCode: Integer;
begin
  Exec('sc', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Result := True;
end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; --- Dashboard (Electron app) ---
Source: "staging\dashboard\*"; DestDir: "{app}\Dashboard"; \
    Flags: recursesubdirs createallsubdirs ignoreversion

; --- Background Service (self-contained .NET Worker) ---
Source: "staging\service\*"; DestDir: "{app}\Service"; \
    Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Dashboard\{#MyDashboardExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Dashboard\{#MyDashboardExe}"; Tasks: desktopicon

[Run]
; Create the Windows Service (auto-start). Quotes around binPath are required by sc.exe.
Filename: "{sys}\sc.exe"; \
    Parameters: "create {#MyServiceName} binPath= ""{app}\Service\{#MyServiceExe}"" start= auto DisplayName= ""Mafia City Anti-Cheat V6 Service"""; \
    Flags: runhidden waituntilterminated; \
    StatusMsg: "Registering anti-cheat service..."

; Give it a description.
Filename: "{sys}\sc.exe"; \
    Parameters: "description {#MyServiceName} ""Background protection service for Mafia City Anti-Cheat V6."""; \
    Flags: runhidden waituntilterminated

; Configure automatic restart on failure (immediate: 1s delay, 3 retries, reset after 24h).
Filename: "{sys}\sc.exe"; \
    Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/1000/restart/1000/restart/1000"; \
    Flags: runhidden waituntilterminated

; Also set failure flag for the OS to treat the service as critical.
Filename: "{sys}\sc.exe"; \
    Parameters: "failureflag {#MyServiceName} 1"; \
    Flags: runhidden waituntilterminated

; Add service directory to Windows Defender exclusion (prevents AV false positives).
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-Command ""Add-MpPreference -ExclusionPath '{app}\Service' -ErrorAction SilentlyContinue"""; \
    Flags: runhidden waituntilterminated

; Create a scheduled-task watchdog that restarts the service if killed (runs every minute as SYSTEM).
; Uses a .ps1 script file (deployed alongside the service) to avoid quoting issues.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-Command ""$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -File ""{app}\Service\watchdog.ps1""'; $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1); $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -Priority 0; Register-ScheduledTask -TaskName '{#MyServiceName}-Watchdog' -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User 'SYSTEM' -Force -ErrorAction SilentlyContinue"""; \
    Flags: runhidden waituntilterminated

; Start the service now.
Filename: "{sys}\sc.exe"; \
    Parameters: "start {#MyServiceName}"; \
    Flags: runhidden waituntilterminated; \
    StatusMsg: "Starting anti-cheat service..."

; Optionally launch the dashboard after install.
Filename: "{app}\Dashboard\{#MyDashboardExe}"; \
    Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and remove the watchdog scheduled task.
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/end /tn ""{#MyServiceName}-Watchdog"""; \
    Flags: runhidden waituntilterminated; RunOnceId: "StopWatchdog"
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/delete /tn ""{#MyServiceName}-Watchdog"" /f"; \
    Flags: runhidden waituntilterminated; RunOnceId: "DelWatchdog"

; Stop and remove the Windows Service before files are deleted.
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; \
    Flags: runhidden waituntilterminated; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; \
    Flags: runhidden waituntilterminated; RunOnceId: "DeleteService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Service\logs"
