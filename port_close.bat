@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - remove the firewall rule that port_open.bat created
rem
rem    port_close.bat
rem
rem  Deletes the inbound rule named by RULE below. It does NOT touch your
rem  router: if you forwarded ports there for friends to connect, remove those
rem  in the router's admin page too (see docs/SERVER_SETUP.md, Part 3).
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
title Cronus - close firewall ports

set "RULE=Cronus JMSv186"

rem --- netsh needs administrator rights; relaunch elevated if we don't have them.
net session >nul 2>&1
if errorlevel 1 (
    echo [i] Administrator rights are required - asking for them...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

netsh advfirewall firewall show rule name="%RULE%" >nul 2>&1
if errorlevel 1 (
    echo [i] No rule named "%RULE%" exists - nothing to remove.
    echo     ^(Either it was already deleted, or the ports were never opened.^)
    goto :halt
)

echo Removing the inbound rule "%RULE%"...
netsh advfirewall firewall delete rule name="%RULE%" >nul
if errorlevel 1 (
    echo [!] Failed to delete the rule.
    goto :halt
)

echo [OK] Rule removed - this PC no longer accepts inbound connections on the
echo      Cronus ports. Playing on 127.0.0.1 still works; remote players do not.
echo.
echo Reminder: any port forwarding you set up on your ROUTER is still active.
echo Remove it there as well if you are done hosting.

:halt
echo.
pause
endlocal
