@echo off
setlocal
echo === PubQuizCreator: Rebuild Database ===
echo.

set ROOT=%~dp0\..
set MIGRATIONS=%ROOT%\PubQuizCreator.Data\Migrations
set DATA_PROJECT=%ROOT%\PubQuizCreator.Data
set WEB_PROJECT=%ROOT%\PubQuizCreator.Web
set CONTAINER=pubquizcreator-db-1
set DB_USER=pubquiz

echo Do you want to delete all existing migrations first?
echo (Useful when the model has changed significantly)
echo.
choice /c YN /m "Delete migrations folder contents [Y/N]"
if %ERRORLEVEL% equ 1 (
    echo.
    echo Deleting migrations...
    del /q "%MIGRATIONS%\*.cs" >nul 2>&1
    echo Done.
)

echo.
echo [1/5] Stopping containers and removing volumes...
docker compose -f "%ROOT%\docker-compose.dev.yml" down -v
if %ERRORLEVEL% neq 0 ( echo ERROR: docker compose down failed & pause & exit /b 1 )

echo.
echo [2/5] Starting containers...
docker compose -f "%ROOT%\docker-compose.dev.yml" up -d
if %ERRORLEVEL% neq 0 ( echo ERROR: docker compose up failed & pause & exit /b 1 )

echo.
echo [3/5] Waiting for PostgreSQL to be ready...
:waitloop
docker exec %CONTAINER% pg_isready -U %DB_USER% >nul 2>&1
if %ERRORLEVEL% neq 0 ( timeout /t 2 /nobreak >nul & goto waitloop )
echo PostgreSQL is ready.

echo.
echo [4/5] Checking migrations...
dir /b "%MIGRATIONS%\*.cs" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo No migrations found -- creating InitialCreate...
    dotnet ef migrations add InitialCreate --project "%DATA_PROJECT%" --startup-project "%WEB_PROJECT%"
    if %ERRORLEVEL% neq 0 ( echo ERROR: migrations add failed & pause & exit /b 1 )
) else (
    echo Migrations found -- skipping migrations add.
)

echo.
echo [5/5] Applying migrations...
dotnet ef database update --project "%DATA_PROJECT%" --startup-project "%WEB_PROJECT%"
if %ERRORLEVEL% neq 0 ( echo ERROR: Migration failed & pause & exit /b 1 )

echo.
echo === Done. Database rebuilt successfully. ===
pause
