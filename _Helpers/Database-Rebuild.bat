@echo off
echo === PubQuizCreator: Rebuild Database ===
echo.

REM Navigate to solution root
cd /d "%~dp0\.."

echo Do you want to delete all existing migrations first?
echo (Useful when the model has changed significantly)
echo.
choice /c YN /m "Delete migrations folder contents [Y/N]"
if %ERRORLEVEL% equ 1 (
    echo.
    echo Deleting migrations...
    del /q "PubQuizCreator.Data\Migrations\*.cs" >nul 2>&1
    echo Done.
)

echo.
echo [1/5] Stopping containers and removing volumes...
docker compose -f docker-compose.dev.yml down -v
if %ERRORLEVEL% neq 0 ( echo ERROR: docker compose down failed & pause & exit /b 1 )

echo.
echo [2/5] Starting containers...
docker compose -f docker-compose.dev.yml up -d
if %ERRORLEVEL% neq 0 ( echo ERROR: docker compose up failed & pause & exit /b 1 )

echo.
echo [3/5] Waiting for PostgreSQL to be ready...
timeout /t 5 /nobreak >nul

echo.
echo [4/5] Checking migrations...
cd PubQuizCreator.Web

dir /b "..\PubQuizCreator.Data\Migrations\*.cs" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo No migrations found -- creating InitialCreate...
    dotnet ef migrations add InitialCreate --project ..\PubQuizCreator.Data
    if %ERRORLEVEL% neq 0 ( echo ERROR: migrations add failed & pause & exit /b 1 )
) else (
    echo Migrations found -- skipping migrations add.
)

echo.
echo [5/5] Applying migrations...
dotnet ef database update --project ..\PubQuizCreator.Data
if %ERRORLEVEL% neq 0 ( echo ERROR: Migration failed & pause & exit /b 1 )

echo.
echo === Done. Database rebuilt successfully. ===
pause
