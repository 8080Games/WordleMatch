@echo off
echo.
echo =====================================================
echo    Starting Chrome with Remote Debugging
echo =====================================================
echo.
echo This will start Chrome with debugging enabled so
echo the fetch-nyt-word script can connect to it.
echo.
echo Leave this window open while running the script.
echo.

REM Try to find Chrome in common locations
set CHROME_PATH="C:\Program Files\Google\Chrome\Application\chrome.exe"
if not exist %CHROME_PATH% set CHROME_PATH="C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"

if not exist %CHROME_PATH% (
    echo ERROR: Could not find Chrome
    echo Please update the path in this script
    pause
    exit /b 1
)

echo Starting Chrome...
echo.
start "" %CHROME_PATH% --remote-debugging-port=9222

echo.
echo Chrome started with debugging on port 9222
echo.
echo You can now run: fetch-nyt-word.bat
echo.
pause
