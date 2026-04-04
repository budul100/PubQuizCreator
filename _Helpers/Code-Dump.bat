@echo off
setlocal

set "SCRIPT=.\Code-Dump.ps1"

if not exist "%SCRIPT%" (
    echo ERROR: Script not found: %SCRIPT%
    pause
    exit /b 1
)

powershell -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo Script exited with error code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)
