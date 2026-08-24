@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - build gamedata.db from a client's .wz files
rem
rem    ingest.bat                    client folder from .env (CRONUS_CLIENT)
rem    ingest.bat "C:\path\client"   explicit client folder (drag & drop works)
rem
rem  Writes the database to CRONUS_GAMEDATA from .env, or gamedata.db next to
rem  this script. Safe to re-run any time (the old database is replaced) -
rem  do it whenever the client's .wz files change. The server must be stopped
rem  first only if it is running FROM the same gamedata.db being replaced.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
title Cronus - game data ingest
set "RC=1"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [!] .NET SDK not found.
    echo     Install .NET SDK 10 from https://dotnet.microsoft.com/download and re-run.
    goto :halt
)

rem --- Client folder: argument first, then .env, then ask.
set "CLIENT=%~1"
if "%CLIENT%"=="" call :readenv CRONUS_CLIENT CLIENT
if "%CLIENT%"=="" (
    echo Where is the JMS v186 client folder ^(the one holding Base.wz, String.wz, ...^)?
    set /p "CLIENT=  client folder: "
)
if "%CLIENT%"=="" (
    echo [!] No client folder given.
    goto :halt
)

if not exist "%CLIENT%\String.wz" (
    echo [!] "%CLIENT%" does not look like a client folder ^(String.wz not found^).
    goto :halt
)

rem --- Output path: .env's CRONUS_GAMEDATA, or gamedata.db here.
call :readenv CRONUS_GAMEDATA OUT
if "%OUT%"=="" set "OUT=%~dp0gamedata.db"

echo.
echo   client : %CLIENT%
echo   output : %OUT%
echo.

if not "%CRONUS_INGEST_NOBUILD%"=="1" (
    echo [1/2] Building the ingest tool...
    dotnet build src\Cronus.Ingest -c Debug --nologo -v quiet
    if errorlevel 1 (
        echo [!] Build failed.
        goto :halt
    )
)

echo [2/2] Ingesting ^(about 20 seconds^)...
dotnet run --project src\Cronus.Ingest -c Debug --no-build -- "%CLIENT%" --out "%OUT%"
if errorlevel 1 (
    echo.
    echo [!] Ingest failed. Check that the folder holds the original JMS v186 .wz files.
    goto :halt
)

echo.
echo [OK] Game data ready: %OUT%
echo      The server picks it up via CRONUS_GAMEDATA in .env ^(restart it if running^).
set "RC=0"

:halt
if "%CRONUS_SETUP_CHAIN%"=="1" exit /b %RC%
echo.
pause
endlocal
exit /b

rem --- Reads KEY=value from .env (whole value, spaces kept; commented lines skipped). ------
:readenv
set "%~2="
if not exist ".env" exit /b
for /f "usebackq tokens=1,* delims==" %%a in (`findstr /b /i /c:"%~1=" ".env"`) do set "%~2=%%b"
exit /b
