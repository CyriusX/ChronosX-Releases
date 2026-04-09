// Helper functions for TimeTrack installer

var
  AutoStartCheckBox: TNewCheckBox;

function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', Version) or
    RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', Version) or
    RegQueryStringValue(HKEY_CURRENT_USER,
    'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', Version);
end;

function ShouldAutoStartDesktop: Boolean;
begin
  Result := AutoStartCheckBox.Checked;
end;

// ── Cleanup helper ──────────────────────────────────────────────────────────
// Runs a hidden command and waits for it. Errors are silently swallowed so
// cleanup never blocks the install.
procedure RunHidden(Exe, Params: String);
var
  ResultCode: Integer;
begin
  Exec(Exe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('RunHidden: ' + Exe + ' ' + Params + ' => ' + IntToStr(ResultCode));
end;

// Kills all running ChronosX / TimeTrack processes, stops the scheduled task,
// and removes the legacy Windows Service. Called before files are copied.
procedure CleanupPreviousInstallation;
var
  Sys: String;
begin
  Sys := ExpandConstant('{sys}');

  Log('CleanupPreviousInstallation: starting');

  // 1. Stop the scheduled task (new architecture)
  RunHidden(Sys + '\WindowsPowerShell\v1.0\powershell.exe',
    '-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command ' +
    '"Stop-ScheduledTask -TaskName ''ChronosX Agent'' -ErrorAction SilentlyContinue"');

  // 2. Stop and delete the legacy Windows Service (old architecture)
  RunHidden(Sys + '\sc.exe', 'stop ChronosXAgent');
  RunHidden(Sys + '\sc.exe', 'delete ChronosXAgent');

  // 3. Kill all remaining TimeTrack / ChronosX processes so files are unlocked
  RunHidden(Sys + '\taskkill.exe', '/F /IM TimeTrack.AgentService.exe /T');
  RunHidden(Sys + '\taskkill.exe', '/F /IM TimeTrack.DesktopHost.exe /T');
  RunHidden(Sys + '\taskkill.exe', '/F /IM ChronosX.exe /T');

  // Give Windows a moment to release file handles
  Sleep(1500);

  Log('CleanupPreviousInstallation: done');
end;
// ────────────────────────────────────────────────────────────────────────────

procedure InitializeWizard;
var
  Page: TWizardPage;
begin
  Page := CreateCustomPage(wpSelectTasks, 'Startup Configuration', 'Configure how ChronosX starts');

  AutoStartCheckBox := TNewCheckBox.Create(Page);
  AutoStartCheckBox.Parent := Page.Surface;
  AutoStartCheckBox.Caption := 'Start ChronosX Desktop when Windows starts';
  AutoStartCheckBox.Checked := True;
  AutoStartCheckBox.Left := ScaleX(0);
  AutoStartCheckBox.Top := ScaleY(10);
  AutoStartCheckBox.Width := Page.SurfaceWidth;
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not IsWebView2Installed then
  begin
    if MsgBox('WebView2 Runtime is not installed. The installer will download and install it automatically.' + #13#10 + #13#10 +
      'Do you want to continue?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

// ── Agent Task registration ─────────────────────────────────────────────────
// Registers the agent as a Task Scheduler task that:
//   - Runs at user logon (Session 1 — the interactive desktop)
//   - Runs with highest available privileges
//   - 10-second start delay so the desktop is ready
// This replaces the old Windows Service (Session 0) where GetForegroundWindow()
// always returns null and window tracking is impossible.
procedure RegisterAgentTask(AppDir: String);
var
  ExePath, WorkDir, PsCmd: String;
  ResultCode: Integer;
begin
  ExePath := AppDir + '\service\TimeTrack.AgentService.exe';
  WorkDir := AppDir + '\service';

  PsCmd :=
    '$u = $env:USERNAME;' +
    '$a = New-ScheduledTaskAction -Execute ''' + ExePath + ''' -WorkingDirectory ''' + WorkDir + ''';' +
    '$t = New-ScheduledTaskTrigger -AtLogOn -User $u;' +
    '$t.Delay = ''PT10S'';' +
    '$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0 -MultipleInstances IgnoreNew -StartWhenAvailable;' +
    '$p = New-ScheduledTaskPrincipal -UserId $u -LogonType Interactive -RunLevel Highest;' +
    'Register-ScheduledTask -TaskName ''ChronosX Agent'' -Action $a -Trigger $t -Settings $s -Principal $p -Force | Out-Null;' +
    'Start-ScheduledTask -TaskName ''ChronosX Agent''';

  if not ShellExec('open',
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command "' + PsCmd + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('WARNING: Could not register agent task. Code: ' + IntToStr(ResultCode))
  else
    Log('Agent task registered and started. Exit: ' + IntToStr(ResultCode));
end;
// ────────────────────────────────────────────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Kill everything BEFORE Inno Setup copies files.
    // If processes are still running, file copy will fail silently or partially.
    CleanupPreviousInstallation;
  end;

  if CurStep = ssPostInstall then
  begin
    RegisterAgentTask(ExpandConstant('{app}'));
    Log('ssPostInstall: agent task registered.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Sys, DataPath: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Sys := ExpandConstant('{sys}');

    // Stop and remove scheduled task
    Exec(Sys + '\WindowsPowerShell\v1.0\powershell.exe',
      '-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command ' +
      '"Stop-ScheduledTask -TaskName ''ChronosX Agent'' -ErrorAction SilentlyContinue; ' +
      'Unregister-ScheduledTask -TaskName ''ChronosX Agent'' -Confirm:$false -ErrorAction SilentlyContinue"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Also clean up legacy service just in case
    Exec(Sys + '\sc.exe', 'stop ChronosXAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(Sys + '\sc.exe', 'delete ChronosXAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Kill processes so files can be deleted
    Exec(Sys + '\taskkill.exe', '/F /IM TimeTrack.AgentService.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(Sys + '\taskkill.exe', '/F /IM TimeTrack.DesktopHost.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    Sleep(1500);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Do you want to remove all ChronosX data including your local database and settings?',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      DataPath := ExpandConstant('{localappdata}\TimeTrack');
      if DirExists(DataPath) then
        DelTree(DataPath, True, True, True);
    end;
  end;
end;
