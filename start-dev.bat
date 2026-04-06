@echo off

REM start-dev.bat
REM Starts all Docker containers required for debugging PubQuizCreator.
REM Run from the solution root directory.

setlocal

set COMPOSE_FILE=docker-compose.dev.yml
set OLLAMA_URL=http://localhost:11434
set OLLAMA_MODEL=nomic-embed-text
set MAX_WAIT=30

REM Sanity check
if not exist %COMPOSE_FILE% (
    echo [ERROR] Cannot find %COMPOSE_FILE%. Run this script from the solution root.
    exit /b 1
)

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo Docker is not running. Please start Docker Desktop first.
    if not "%1"=="--no-pause" pause
    exit /b 1
)

REM Start containers
echo [INFO] Starting containers...
docker compose -f %COMPOSE_FILE% up -d
if errorlevel 1 (
    echo [ERROR] docker compose failed.
    exit /b 1
)

REM Wait for PostgreSQL
echo [INFO] Waiting for PostgreSQL to be ready...
set /a elapsed=0
:wait_db
timeout /t 2 /nobreak >nul
set /a elapsed+=2
docker compose -f %COMPOSE_FILE% exec -T db pg_isready -U pubquiz -d pubquiz >nul 2>&1
if not errorlevel 1 goto db_ready
if %elapsed% geq %MAX_WAIT% (
    echo [ERROR] PostgreSQL did not become ready within %MAX_WAIT% seconds.
    exit /b 1
)
goto wait_db
:db_ready
echo [OK] PostgreSQL ready.

REM Wait for Ollama
echo [INFO] Waiting for Ollama to be ready...
set /a elapsed=0
:wait_ollama
timeout /t 2 /nobreak >nul
set /a elapsed+=2
docker compose -f %COMPOSE_FILE% exec -T ollama ollama list >nul 2>&1
if not errorlevel 1 goto ollama_ready
if %elapsed% geq %MAX_WAIT% (
    echo [WARN] Ollama did not respond within %MAX_WAIT% seconds. Continuing without embedding.
    goto summary
)
goto wait_ollama
:ollama_ready
echo [OK] Ollama ready.

REM Ensure embedding model is available
echo [INFO] Checking model '%OLLAMA_MODEL%'...
docker compose -f %COMPOSE_FILE% exec -T ollama ollama list 2>nul | findstr /i "%OLLAMA_MODEL%" >nul
if errorlevel 1 (
    echo [INFO] Model not found -- pulling '%OLLAMA_MODEL%'. This may take a while...
    docker compose -f %COMPOSE_FILE% exec ollama ollama pull %OLLAMA_MODEL%
    echo [OK] Model ready.
) else (
    echo [OK] Model '%OLLAMA_MODEL%' already present.
)

:summary
echo.
echo [OK] All services running:
echo       PostgreSQL  --^>  localhost:5433  ^(DB: pubquiz, User: pubquiz^)
echo       Ollama      --^>  localhost:11434
echo.
echo [INFO] Start the app with:
echo         cd PubQuizCreator.Web ^&^& dotnet run
echo         -- or press F5 in Visual Studio / Rider
echo.

if not "%1"=="--no-pause" pause

endlocal
BATCHEOF

echo "Done"