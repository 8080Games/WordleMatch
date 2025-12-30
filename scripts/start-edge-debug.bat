@echo off
echo.
echo =====================================================
echo    Starting Edge with Remote Debugging
echo =====================================================
echo.
echo This will start Edge with debugging enabled so
echo the fetch-nyt-word script can connect to it.
echo.
echo Leave this window open while running the script.
echo.

REM Try to find Edge in common locations
set EDGE_PATH="C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if not exist %EDGE_PATH% set EDGE_PATH="C:\Program Files\Microsoft\Edge\Application\msedge.exe"

if not exist %EDGE_PATH% (
    echo ERROR: Could not find Microsoft Edge
    echo Please update the path in this script
    pause
    exit /b 1
)

echo Starting Edge...
echo.
start "" %EDGE_PATH% --remote-debugging-port=9222 --user-data-dir="%TEMP%\edge-debug-profile"

echo.
echo Edge started with debugging on port 9222
echo.
echo You can now run: fetch-nyt-word.bat
echo.
echo Note: This uses a separate Edge profile. You may need to log into NYT.
echo.
pause
