@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - JMS v186 server launcher
rem
rem    run-server.bat            build + run (Debug: keeps the existing cronus.db)
rem    run-server.bat Release    build + run in Release
rem
rem  Settings come from the .env file next to this script (copy .env.example to
rem  .env and edit). Close the window or press Ctrl+C to stop the server.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
title Cronus - JMS v186 server

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [!] .NET SDK not found.
    echo     Install .NET SDK 10 from https://dotnet.microsoft.com/download and re-run.
    goto :halt
)

if not exist ".env" (
    if exist ".env.example" (
        echo [i] No .env yet - creating one from .env.example.
        copy /y ".env.example" ".env" >nul
        echo     Edit .env to set CRONUS_HOST / data paths, then re-run for your own settings.
        echo.
    )
)

echo [1/2] Building (%CONFIG%)...
dotnet build Cronus.slnx -c %CONFIG% --nologo -v quiet
if errorlevel 1 (
    echo.
    echo [!] Build failed. If the error mentions a locked file, another Cronus server
    echo     is still running - close it and try again.
    goto :halt
)

echo [2/2] Starting the server...
echo.
dotnet run --project src\Cronus.Server.Host -c %CONFIG% --no-build
set "EXITCODE=%ERRORLEVEL%"

echo.
if not "%EXITCODE%"=="0" (
    echo [!] The server exited with code %EXITCODE%. The newest file in
    echo     src\Cronus.Server.Host\bin\%CONFIG%\net10.0\logs has the details.
) else (
    echo [i] Server stopped.
)

:halt
echo.
pause
endlocal
