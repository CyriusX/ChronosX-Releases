; File definitions for TimeTrack installer
; Uses relative paths from project root

#define ProjectRoot ".."

; AgentService - Windows Service
Source: "{#ProjectRoot}\build\publish\AgentService\TimeTrack.AgentService.exe"; DestDir: "{app}\service"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\AgentService\*.dll"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\*.json"; DestDir: "{app}\service"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\AgentService\createdump.exe"; DestDir: "{app}\service"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\runtimes\*"; DestDir: "{app}\service\runtimes"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

; Language folders
Source: "{#ProjectRoot}\build\publish\AgentService\cs\*"; DestDir: "{app}\service\cs"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\de\*"; DestDir: "{app}\service\de"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\es\*"; DestDir: "{app}\service\es"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\fr\*"; DestDir: "{app}\service\fr"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\it\*"; DestDir: "{app}\service\it"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\ja\*"; DestDir: "{app}\service\ja"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\ko\*"; DestDir: "{app}\service\ko"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\pt-BR\*"; DestDir: "{app}\service\pt-BR"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\ru\*"; DestDir: "{app}\service\ru"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\zh-Hans\*"; DestDir: "{app}\service\zh-Hans"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\AgentService\zh-Hant\*"; DestDir: "{app}\service\zh-Hant"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

; DesktopHost - UI Container
Source: "{#ProjectRoot}\build\publish\DesktopHost\TimeTrack.DesktopHost.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\DesktopHost\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#ProjectRoot}\build\publish\DesktopHost\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\DesktopHost\*.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\DesktopHost\*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\DesktopHost\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs

; Update tool - Self-relocating update utility
Source: "{#ProjectRoot}\build\publish\Update\update.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\Update\update.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\Update\update.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectRoot}\build\publish\Update\update.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

; WebView2 Runtime (bootstrapper - will be downloaded if not present)
Source: "resources\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2Installed

; React UI (Desktop - timetrack-ui)
; Copia para {app}\ui\dist\ para compatibilidade com GetUiBundlePath() no MainForm.cs
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\index.html"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\favicon.ico"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\favicon-16x16.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\favicon-32x32.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\favicon-48x48.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\icon-128.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\icon-256.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\icon-512.png"; DestDir: "{app}\ui\dist"; Flags: ignoreversion
; Assets
Source: "{#ProjectRoot}\src\ui\timetrack-ui\dist\assets\*"; DestDir: "{app}\ui\dist\assets"; Flags: ignoreversion recursesubdirs
