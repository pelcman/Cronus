@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - stop the World, Login and Channel processes started by run-server.bat
rem  (and the old single-process host, if one is still around).
rem ---------------------------------------------------------------------------
setlocal
echo Stopping Cronus servers...
for %%P in (Cronus.Server.Channel.exe Cronus.Server.Login.exe Cronus.Server.World.exe Cronus.Server.Host.exe) do (
    tasklist /FI "IMAGENAME eq %%P" 2>nul | find /I "%%P" >nul
    if not errorlevel 1 (
        taskkill /IM %%P /F >nul 2>&1
        echo   stopped %%P
    )
)
echo Done.
endlocal
