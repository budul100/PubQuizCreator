@echo off
setlocal enabledelayedexpansion

set WEB_PROJECT=PubQuizCreator.Web

chcp 65001 >nul

cd /d "%~dp0\..\%WEB_PROJECT%" 2>nul || (
    echo ERROR: %WEB_PROJECT% folder not found.
    echo Run this script from the solution root.
    pause
    exit /b 1
)

:: --- Ensure UserSecretsId exists ---
echo Initializing user secrets (safe to run multiple times)...
dotnet user-secrets init >nul 2>&1
echo Done.
echo.

echo ======================
echo   User Secrets Setup
echo ======================
echo.
echo This script sets local dev secrets via dotnet user-secrets.
echo Run from the solution root or %WEB_PROJECT% folder.
echo Press ENTER to skip a value (keeps existing secret).
echo.

:: --- Media:StoragePath ---
echo [1/2] Media:StoragePath
echo     Local folder where uploaded media files are stored.
echo     Example: C:\Users\XYZ\Media
set /p MEDIA_PATH="    Value: "
if not "!MEDIA_PATH!"=="" (
    dotnet user-secrets set "Media:StoragePath" "!MEDIA_PATH!"
    echo     Set.
) else (
    echo     Skipped.
)
echo.

:: --- ConnectionStrings:Default ---
echo [2/2] ConnectionStrings:Default
echo     PostgreSQL connection string for local dev.
echo     Example: Host=localhost;Port=5433;Database=pubquiz;Username=postgres;Password=postgres
set /p CONN_STR="    Value: "
if not "!CONN_STR!"=="" (
    dotnet user-secrets set "ConnectionStrings:Default" "!CONN_STR!"
    echo     Set.
) else (
    echo     Skipped.
)
echo.

echo ============================================
echo  Done. Current secrets:
echo ============================================
dotnet user-secrets list
echo.
pause
