@echo off
REM Daily Wordle data update - Runs automatically via Task Scheduler
REM Adds tomorrow's word (and back-fills any missed days) from the NYT puzzle API,
REM then commits + pushes. Shows a Windows toast on failure; logs to logs\

cd /d "%~dp0"

REM Create logs directory if it doesn't exist
if not exist "logs" mkdir logs

REM Set log file with timestamp
set LOGFILE=logs\fetch-%date:~-4,4%%date:~-10,2%%date:~-7,2%-%time:~0,2%%time:~3,2%.log
set LOGFILE=%LOGFILE: =0%

python daily-update.py >> "%LOGFILE%" 2>&1

exit /b %ERRORLEVEL%
