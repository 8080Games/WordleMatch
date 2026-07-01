@echo off
REM Daily Wordle Word Fetcher - Runs automatically via Task Scheduler
REM This script starts Edge with debugging, runs the fetch script, and logs results

cd /d "%~dp0"

REM Create logs directory if it doesn't exist
if not exist "logs" mkdir logs

REM Set log file with timestamp
set LOGFILE=logs\fetch-%date:~-4,4%%date:~-10,2%%date:~-7,2%-%time:~0,2%%time:~3,2%.log
set LOGFILE=%LOGFILE: =0%

echo ========================================== >> "%LOGFILE%" 2>&1
echo Daily Wordle Fetch - %date% %time% >> "%LOGFILE%" 2>&1
echo ========================================== >> "%LOGFILE%" 2>&1

REM Sync with remote first so add-word.mjs's push can't be rejected
echo Pulling latest from remote... >> "%LOGFILE%" 2>&1
git pull --ff-only >> "%LOGFILE%" 2>&1

REM Start Edge with debugging
echo Starting Edge with debugging... >> "%LOGFILE%" 2>&1
start "" /min cmd /c "start-edge-debug.bat"

REM Wait for Edge to start up
echo Waiting 10 seconds for Edge to start... >> "%LOGFILE%" 2>&1
timeout /t 10 /nobreak >> "%LOGFILE%" 2>&1

REM Run the fetch script
echo Running fetch script... >> "%LOGFILE%" 2>&1
python fetch-nyt-word.py >> "%LOGFILE%" 2>&1

echo. >> "%LOGFILE%" 2>&1
echo ========================================== >> "%LOGFILE%" 2>&1
echo Fetch completed - %date% %time% >> "%LOGFILE%" 2>&1
echo ========================================== >> "%LOGFILE%" 2>&1

exit /b 0
