@echo off
setlocal

set CONTAINER=pubquizcreator-db-1
set DB=pubquiz
set USER=pubquiz
set BACKUP_DIR=%~dp0backups

echo === PubQuizCreator: Restore ===
echo.
echo Available backups:
echo.
dir /b "%BACKUP_DIR%\*.dump" 2>nul
if %ERRORLEVEL% neq 0 ( echo No backups found in %BACKUP_DIR% & pause & exit /b 1 )

echo.
set /p FILENAME=Enter filename (without path): 

if not exist "%BACKUP_DIR%\%FILENAME%" (
    echo ERROR: File not found: %BACKUP_DIR%\%FILENAME%
    pause & exit /b 1
)

echo.
echo WARNING: This will drop and recreate the database "%DB%".
choice /c YN /m "Continue?"
if %ERRORLEVEL% equ 2 ( echo Aborted. & pause & exit /b 0 )

echo.
echo [1/3] Dropping database...
docker exec %CONTAINER% psql -U %USER% -c "DROP DATABASE IF EXISTS %DB%;"
if %ERRORLEVEL% neq 0 ( echo ERROR: Drop failed & pause & exit /b 1 )

echo [2/3] Creating database...
docker exec %CONTAINER% psql -U %USER% -c "CREATE DATABASE %DB% OWNER %USER%;"
if %ERRORLEVEL% neq 0 ( echo ERROR: Create failed & pause & exit /b 1 )

echo [3/3] Restoring...
docker exec -i %CONTAINER% pg_restore -U %USER% -d %DB% --no-owner < "%BACKUP_DIR%\%FILENAME%"
if %ERRORLEVEL% neq 0 ( echo ERROR: Restore failed & pause & exit /b 1 )

echo.
echo Done. Database restored from %FILENAME%.
pause
