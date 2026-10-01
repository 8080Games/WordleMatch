@echo off
REM Daily WordleMatch production check - Runs automatically via Task Scheduler
REM Shows a Windows toast if anything fails; full results go to logs\

cd /d "%~dp0"

if not exist "logs" mkdir logs

set LOGFILE=logs\prod-check-%date:~-4,4%%date:~-10,2%%date:~-7,2%-%time:~0,2%%time:~3,2%.log
set LOGFILE=%LOGFILE: =0%

python check-production.py >> "%LOGFILE%" 2>&1

exit /b %ERRORLEVEL%
