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

  // Check for WebView2
  if not IsWebView2Installed then
  begin
    if MsgBox('WebView2 Runtime is not installed. The installer will download and install it automatically.' + #13#10 + #13#10 +
      'Do you want to continue?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    Log('TimeTrack installation completed successfully');
    Log('Service installed at: ' + ExpandConstant('{app}\service\{#AgentServiceExe}'));
    Log('Desktop installed at: ' + ExpandConstant('{app}\{#MyAppExeName}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask user if they want to remove data
    if MsgBox('Do you want to remove all ChronosX data including your local database and settings?',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Remove SQLite DB and settings (actual data directory used by the agent)
      DataPath := ExpandConstant('{localappdata}\TimeTrack');
      if DirExists(DataPath) then
        DelTree(DataPath, True, True, True);
    end;
  end;
end;
