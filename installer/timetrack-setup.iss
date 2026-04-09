; TimeTrack Desktop Installer - Main Script
; Requires Inno Setup 6.x
; https://jrsoftware.org/isinfo.php

#define MyAppName "ChronosX"
#define MyAppPublisher "Cyrius"
#define MyAppURL "https://cyrius.com/timetrack"
#define MyAppExeName "TimeTrack.DesktopHost.exe"
#define AgentServiceExe "TimeTrack.AgentService.exe"
#define ServiceName "ChronosX Agent"

; Version - updated by build script
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppVersionStr StringChange(MyAppVersion, ".", "") + ".0"

[Setup]
AppId={{8F3D9B2A-1C4E-4F6B-9D8A-3E5C7B2F1A6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\build\installer
OutputBaseFilename=ChronosX-Setup-{#MyAppVersion}
SetupIconFile=resources\setup-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersionStr}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=ChronosX Time Tracking Installer
VersionInfoCopyright=Copyright (C) 2024 Cyrius
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersionStr}
; Close applications before installing
CloseApplicationsFilter=*.exe,*.dll
; Create uninstall log
UninstallLogMode=append
; License and info pages
; LicenseFile=..\LICENSE
; InfoBeforeFile=..\docs\INSTALL-README.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Dirs]
Name: "{app}\service"; Permissions: admins-modify
Name: "{app}\ui"; Permissions: admins-modify
Name: "{app}\logs"; Permissions: admins-modify
Name: "{localappdata}\Cyrius\TimeTrack"; Permissions: users-modify
Name: "{localappdata}\Cyrius\TimeTrack\logs"; Permissions: users-modify

[Files]
#include "includes\files.iss"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Launch app after install (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Kill running processes so Inno Setup can delete their files
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM TimeTrack.AgentService.exe /T"; Flags: runhidden waituntilterminated; RunOnceId: "KillAgent"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM TimeTrack.DesktopHost.exe /T"; Flags: runhidden waituntilterminated; RunOnceId: "KillDesktop"
; Remove scheduled task
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Unregister-ScheduledTask -TaskName 'ChronosX Agent' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTask"

[Registry]
#include "includes\registry.iss"

[Code]
#include "includes\code.iss"
