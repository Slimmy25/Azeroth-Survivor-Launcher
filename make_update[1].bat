@echo off
rem Writes update.json for a new game version. Asks its questions in this window.
cd /d "%~dp0"
where python >nul 2>nul
if errorlevel 1 (
    echo Python was not found. Install it from https://www.python.org/downloads/
    pause
    exit /b 1
)
python make_update.py %*
if not "%~1"=="" pause
