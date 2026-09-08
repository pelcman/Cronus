@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - JMS v186 server launcher (three processes, like Maple2's start.bat)
rem
rem    run-server.bat            build once, then start World / Login / Channel
rem    run-server.bat Release    the same in Release
rem
rem  World   - the hub the other two register with (gRPC on 127.0.0.1:8585)
rem  Login   - port 8484: authentication, world/channel list, character select
rem  Channel - ports 7575.. (CRONUS_CHANNELS of them) + the cash shop: the game
rem
rem  Settings come from the .env file next to this script (copy .env.example to
rem  .env and edit). With Windows Terminal installed the three run as tabs of one
rem  window; otherwise as three console windows. stop-server.bat stops them all.
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
    echo [!] Build failed. If the error mentions a locked file, a Cronus server
    echo     is still running - run stop-server.bat and try again.
    goto :halt
)

set "WORLD=src\Cronus.Server.World\bin\%CONFIG%\net10.0\Cronus.Server.World.exe"
set "LOGIN=src\Cronus.Server.Login\bin\%CONFIG%\net10.0\Cronus.Server.Login.exe"
set "CHANNEL=src\Cronus.Server.Channel\bin\%CONFIG%\net10.0\Cronus.Server.Channel.exe"

echo [2/2] Starting World, Login and Channel...
echo.
rem The processes run from the repo root so .env and relative data paths resolve.
rem World first; Login and Channel retry for 15 s until it answers.
where wt >nul 2>&1
if %errorlevel%==0 (
    wt -d "%CD%" --title "Cronus World" cmd /k "%WORLD%" ; ^
       nt -d "%CD%" --title "Cronus Login" cmd /k "%LOGIN%" ; ^
       nt -d "%CD%" --title "Cronus Channel" cmd /k "%CHANNEL%"
) else (
    start "Cronus World" /d "%CD%" cmd /k "%WORLD%"
    start "Cronus Login" /d "%CD%" cmd /k "%LOGIN%"
    start "Cronus Channel" /d "%CD%" cmd /k "%CHANNEL%"
)

echo [i] Three windows/tabs opened: World, Login, Channel. Point the client at port 8484.
echo     Logs: src\Cronus.Server.^<World^|Login^|Channel^>\bin\%CONFIG%\net10.0\logs\
echo     To stop everything: stop-server.bat
goto :end

:halt
echo.
pause
:end
endlocal
