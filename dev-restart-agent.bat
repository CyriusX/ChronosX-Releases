@echo off
set ROOT=%~dp0
start "AgentService" cmd /k "cd /d %ROOT% && dotnet run --project src\agent\TimeTrack.AgentService"
start "DesktopHost" cmd /k "cd /d %ROOT% && dotnet run --project src\agent\TimeTrack.DesktopHost"
