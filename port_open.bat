@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - open the Windows Firewall for the server's ports
rem
rem    port_open.bat                     ports from .env (login 8484, channel 7575)
rem    port_open.bat 8484 7575           override the login / channel base ports
rem    port_open.bat 8484 7575 4         ...and the channel count
rem
rem  Creates ONE inbound TCP rule named by RULE below, replacing any existing
rem  rule of that name - so re-running it after changing CRONUS_CHANNELS is safe.
rem  Remove it again with port_close.bat.
rem
rem  This only opens THIS PC's firewall. Friends on the internet also need the
rem  same ports forwarded on your router (see docs/SERVER_SETUP.md, Part 3).
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
title Cronus - open firewall ports

set "RULE=Cronus JMSv186"

rem --- netsh needs administrator rights; relaunch elevated if we don't have them.
net session >nul 2>&1
if errorlevel 1 (
    echo [i] Administrator rights are required - asking for them...
    if "%~1"=="" (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    ) else (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%*' -Verb RunAs"
    )
    exit /b
)

rem --- Ports: arguments win, otherwise .env, otherwise the server's defaults.
set "LOGIN=%~1"
set "CHBASE=%~2"
set "CHANNELS=%~3"
if "%LOGIN%"==""  set "LOGIN=8484"
if "%CHBASE%"=="" set "CHBASE=7575"

if "%CHANNELS%"=="" call :readenv CRONUS_CHANNELS CHANNELS
if "%CHANNELS%"=="" set "CHANNELS=2"
rem Guard the arithmetic: the server itself only accepts 1-8 channels.
echo %CHANNELS%| findstr /r /c:"^[1-8]$" >nul
if errorlevel 1 (
    echo [!] CRONUS_CHANNELS="%CHANNELS%" is not a number from 1 to 8 - using 2.
    set "CHANNELS=2"
)

call :readenv CRONUS_NX NX

set /a CHLAST=%CHBASE% + %CHANNELS% - 1
set /a CASH=%CHBASE% + %CHANNELS%

set "PORTS=%LOGIN%"
if %CHANNELS% GEQ 2 (
    set "PORTS=%PORTS%,%CHBASE%-%CHLAST%"
) else (
    set "PORTS=%PORTS%,%CHBASE%"
)

rem CRONUS_NX=0 disables the cash shop, so its port is not needed.
set "CASHNOTE=cash shop  : TCP %CASH%"
if "%NX%"=="0" (
    set "CASH="
    set "CASHNOTE=cash shop  : disabled (CRONUS_NX=0) - port not opened"
) else (
    set "PORTS=%PORTS%,%CASH%"
)

echo.
echo   login      : TCP %LOGIN%
if %CHANNELS% GEQ 2 (echo   channels   : TCP %CHBASE%-%CHLAST%  ^(%CHANNELS% channels^)) else (echo   channels   : TCP %CHBASE%  ^(1 channel^))
echo   %CASHNOTE%
echo.
echo   rule name  : %RULE%
echo   local ports: %PORTS%
echo.

rem --- Replace any previous rule of the same name, then add the new one.
netsh advfirewall firewall delete rule name="%RULE%" >nul 2>&1
netsh advfirewall firewall add rule name="%RULE%" dir=in action=allow protocol=TCP localport=%PORTS% profile=any description="Cronus JMS v186 private server - login, game channels and cash shop." >nul
if errorlevel 1 (
    echo [!] Failed to create the firewall rule.
    goto :halt
)

echo [OK] Inbound rule created.
echo.
netsh advfirewall firewall show rule name="%RULE%" | findstr /i "Rule Name Enabled Direction Protocol LocalPort Action"
echo.
echo Next steps for playing with friends over the internet:
echo   1. Forward the SAME ports on your router to this PC's LAN IP:
for /f "tokens=2 delims=:" %%i in ('ipconfig ^| findstr /i /c:"IPv4"') do echo        LAN IP:%%i
echo   2. Set CRONUS_HOST in .env to your PUBLIC IP (or a DDNS hostname).
echo   3. Have a friend (or a phone on mobile data) check it:
echo        Test-NetConnection ^<your-public-ip^> -Port %LOGIN%
echo.
echo Undo this with port_close.bat.

:halt
echo.
pause
endlocal
exit /b

rem --- Reads KEY=value from .env into a variable, ignoring commented lines. -----
:readenv
set "%~2="
if not exist ".env" exit /b
for /f "usebackq tokens=1,* delims==" %%a in (`findstr /b /i /c:"%~1=" ".env"`) do set "_v=%%b"
if not defined _v exit /b
rem strip a trailing inline comment / stray spaces
for /f "tokens=1" %%x in ("%_v%") do set "%~2=%%x"
set "_v="
exit /b
