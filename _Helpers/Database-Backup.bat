@echo off
setlocal

set BACKUP_DIR=%HiDrive%\Veranstaltungen\PubQuiz\Archiv\Backups

set CONTAINER=pubquizcreator-db-1
set DB=pubquiz
set USER=pubquiz

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set TIMESTAMP=%%i
set FILENAME=pubquiz_%TIMESTAMP%.dump

echo === PubQuizCreator: Backup ===
echo Target: %BACKUP_DIR%\%FILENAME%
echo.

docker exec %CONTAINER% pg_dump -U %USER% -Fc %DB% > "%BACKUP_DIR%\%FILENAME%"
if %ERRORLEVEL% neq 0 ( echo ERROR: Backup failed & pause & exit /b 1 )

echo Done. Backup saved to:
echo %BACKUP_DIR%\%FILENAME%
pause
