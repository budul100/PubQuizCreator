@echo off
setlocal

set BACKUP_DIR=%HiDrive%\Veranstaltungen\PubQuiz\Archiv\Backups

set CONTAINER=pubquizcreator-db-1
set DB=pubquiz
set USER=pubquiz

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f "tokens=1-4 delims=./- " %%a in ("%date%") do set DATUM=%%c%%b%%a
for /f "tokens=1-2 delims=:." %%a in ("%time: =0%") do set ZEIT=%%a%%b

set FILENAME=pubquiz_%DATUM%_%ZEIT%.dump

echo === PubQuizCreator: Backup ===
echo Target: %BACKUP_DIR%\%FILENAME%
echo.

docker exec %CONTAINER% pg_dump -U %USER% -Fc %DB% > "%BACKUP_DIR%\%FILENAME%"
if %ERRORLEVEL% neq 0 ( echo ERROR: Backup failed & pause & exit /b 1 )

echo Done. Backup saved to:
echo %BACKUP_DIR%\%FILENAME%
pause
