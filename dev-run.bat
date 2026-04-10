@echo off
set ROOT=%~dp0

echo [1/4] Starting Backend (localhost:5000)...
start "Backend API" cmd /k "cd /d %ROOT% && dotnet run --project src\backend\TimeTrack.Api"

echo [2/4] Starting Web UI (localhost:5174)...
start "Web UI" cmd /k "cd /d %ROOT%src\ui\timetrack-web && npm run dev"

echo [3/4] Starting Desktop UI (localhost:5173)...
start "Desktop UI" cmd /k "cd /d %ROOT%src\ui\timetrack-ui && npm run dev"

echo [4/4] Starting AgentService (-> localhost:5000)...
start "AgentService" cmd /k "cd /d %ROOT% && dotnet run --project src\agent\TimeTrack.AgentService"

echo Done. DesktopHost launch separately requires elevation.
echo Run this in an elevated terminal: dotnet run --project src\agent\TimeTrack.DesktopHost
