@echo off
echo Updating ChronosX UI files...
xcopy /E /Y "C:\Projetos\TimeTracking\src\ui\timetrack-ui\dist\*" "C:\Program Files\ChronosX\ui\dist\"
if %ERRORLEVEL% EQU 0 (
    echo.
    echo UI files updated successfully!
    echo Restarting DesktopHost...
    taskkill /F /IM TimeTrack.DesktopHost.exe 2>nul
    timeout /t 2 /nobreak >nul
    start "" "C:\Program Files\ChronosX\TimeTrack.DesktopHost.exe"
    echo Done!
) else (
    echo.
    echo ERROR: Failed to copy files. Make sure you ran this as Administrator.
)
pause
