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

echo Checking user secrets...
dotnet user-secrets list >nul 2>&1 || (
    echo ERROR: No UserSecretsId found in project. Run 'dotnet user-secrets init' manually first.
    pause
    exit /b 1
)
echo OK.
echo.

echo ======================
echo   User Secrets Setup
echo ======================
echo.
echo This script sets local dev secrets via dotnet user-secrets.
echo Press ENTER to keep the existing value.
echo.

:: --- Media:StoragePath ---
call :GetSecret "Media:StoragePath" CURRENT_VAL
echo [1/5] Media:StoragePath
echo     Local folder where uploaded media files are stored.
echo     Example: C:\Users\XYZ\Media
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "Media:StoragePath" "!NEW_VAL!"
    echo     Set.
) else (
    echo     Kept.
)
echo.

:: --- Export:TemplatesPath ---
call :GetSecret "Export:TemplatesPath" CURRENT_VAL
echo [2/5] Export:TemplatesPath
echo     Local folder where PPTX template files are stored.
echo     Example: C:\Users\XYZ\Templates
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "Export:TemplatesPath" "!NEW_VAL!"
    echo     Set.
) else (
    echo     Kept.
)
echo.

:: --- ConnectionStrings:Default ---
call :GetSecret "ConnectionStrings:Default" CURRENT_VAL
echo [3/5] ConnectionStrings:Default
echo     PostgreSQL connection string for local dev.
echo     Example: Host=localhost;Port=5433;Database=pubquiz;Username=postgres;Password=postgres
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "ConnectionStrings:Default" "!NEW_VAL!"
    echo     Set.
) else (
    echo     Kept.
)
echo.

:: --- Auth:Username ---
call :GetSecret "Auth:Username" CURRENT_VAL
echo [4/5] Auth:Username
echo     Username for local dev login.
echo     Example: admin
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "Auth:Username" "!NEW_VAL!"
    echo     Set.
) else (
    echo     Kept.
)
echo.

:: --- Auth:Password ---
call :GetSecret "Auth:Password" CURRENT_VAL
echo [5/5] Auth:Password
echo     Password for local dev login.
echo     Example: dev123
if defined CURRENT_VAL (echo     Current: !CURRENT_VAL!) else (echo     Current: ^(not set^))
set /p NEW_VAL="    Value: "
if not "!NEW_VAL!"=="" (
    dotnet user-secrets set "Auth:Password" "!NEW_VAL!"
    echo     Set.
) else (
    echo     Kept.
)

echo ============================================
echo  Done. Current secrets:
echo ============================================
dotnet user-secrets list
echo.
pause
exit /b 0


:: ============================================
:: Subroutine: read current value for a key
:: Usage: call :GetSecret "Key:Name" VARNAME
:: ============================================
:GetSecret
set "_KEY=%~1"
set "%~2="
for /f "tokens=1,* delims==" %%A in ('dotnet user-secrets list 2^>nul ^| findstr /b "%_KEY% ="') do (
    set "_RAW=%%B"
    if defined _RAW set "%~2=!_RAW:~1!"
)
goto :eof
