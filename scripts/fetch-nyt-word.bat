@echo off
echo.
echo =====================================================
echo    NYT WORDLE WORD FETCHER
echo =====================================================
echo.
echo This will open a browser to fetch tomorrow's Wordle
echo word from the New York Times review article.
echo.

cd /d "%~dp0"

REM Check if in scripts directory
if not exist "fetch-nyt-word.py" (
    echo ERROR: Please run this from the scripts directory
    pause
    exit /b 1
)

REM Run the Python script
python fetch-nyt-word.py

echo.
pause
