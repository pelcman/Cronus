@echo off
rem ---------------------------------------------------------------------------
rem  Cronus - first-time server setup
rem
rem    setup.bat                     interactive (asks for the client folder)
rem    setup.bat "C:\path\client"    client folder given up front (drag & drop)
rem
rem  What it does, in order:
rem    1. checks the .NET SDK
rem    2. creates .env from .env.example (kept if it already exists)
rem    3. records your client folder in .env (CRONUS_CLIENT / CRONUS_GAMEDATA)
rem    4. records the MySQL connection in .env (host / port / user / password;
rem       database Cronus186 is created by the server on first start)
rem    5. builds the whole solution
rem    6. builds gamedata.db from the client's .wz files (via ingest.bat)
rem  After it finishes: run-server.bat starts the server, port_open.bat opens
rem  the firewall for friends. Details: docs\SERVER_SETUP.md
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
title Cronus - first-time setup

echo ============================================
echo   Cronus - JMS v186 server: first-time setup
echo ============================================
echo.

rem --- 1. .NET SDK -----------------------------------------------------------
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [!] .NET SDK not found.
    echo     Install .NET SDK 10 from https://dotnet.microsoft.com/download
    echo     then run setup.bat again.
    goto :halt
)
for /f "delims=" %%v in ('dotnet --version') do set "SDKVER=%%v"
echo [1/6] .NET SDK %SDKVER% found.

rem --- 2. .env ---------------------------------------------------------------
if exist ".env" (
    echo [2/6] .env already exists - keeping it.
) else (
    if not exist ".env.example" (
        echo [!] .env.example is missing - is this the repo root?
        goto :halt
    )
    copy /y ".env.example" ".env" >nul
    echo [2/6] Created .env from .env.example.
)

rem --- 3. client folder ------------------------------------------------------
set "CLIENT=%~1"
if "%CLIENT%"=="" call :readenv CRONUS_CLIENT CLIENT
if not "%CLIENT%"=="" if exist "%CLIENT%\String.wz" goto :haveclient

echo.
echo Where is the JMS v186 client folder? It is the folder that holds the
echo game's .wz files: Base.wz, String.wz, Map.wz, ...
echo ^(Tip: you can also drag the folder onto setup.bat next time.^)
set /p "CLIENT=  client folder: "

:haveclient
if "%CLIENT%"=="" (
    echo [!] No client folder given.
    goto :halt
)
if not exist "%CLIENT%\String.wz" (
    echo [!] "%CLIENT%" does not look like a client folder ^(String.wz not found^).
    goto :halt
)

rem Record it in .env: replace existing CRONUS_CLIENT/CRONUS_GAMEDATA lines
rem (commented or not) or append them. PowerShell handles the line editing; the
rem path travels via an environment variable so spaces and quotes survive.
set "SETUP_CLIENT=%CLIENT%"
powershell -NoProfile -Command ^
  "$utf8 = New-Object System.Text.UTF8Encoding $false;" ^
  "$env_ = [System.IO.File]::ReadAllText('.env', $utf8);" ^
  "$client = $env:SETUP_CLIENT; $db = (Resolve-Path '.').Path + '\gamedata.db';" ^
  "if ($env_ -match '(?m)^#?CRONUS_CLIENT=.*$') { $env_ = $env_ -replace '(?m)^#?CRONUS_CLIENT=.*$', ('CRONUS_CLIENT=' + $client) } else { $env_ = $env_.TrimEnd() + \"`r`nCRONUS_CLIENT=$client\" };" ^
  "if ($env_ -match '(?m)^#?CRONUS_GAMEDATA=.*$') { $env_ = $env_ -replace '(?m)^#?CRONUS_GAMEDATA=.*$', ('CRONUS_GAMEDATA=' + $db) } else { $env_ = $env_.TrimEnd() + \"`r`nCRONUS_GAMEDATA=$db`r`n\" };" ^
  "[System.IO.File]::WriteAllText('.env', $env_, $utf8)"
if errorlevel 1 (
    echo [!] Could not update .env - edit it by hand: set CRONUS_CLIENT to your client folder.
    goto :halt
)
echo [3/6] Client folder recorded in .env:
echo        %CLIENT%

rem --- 4. MySQL --------------------------------------------------------------
echo.
echo MySQL 8 stores accounts and characters ^(the database Cronus186 is created
echo by the server on first start^). Press Enter to keep a default.
call :readenv CRONUS_DB_HOST DBHOST
call :readenv CRONUS_DB_PORT DBPORT
call :readenv CRONUS_DB_NAME DBNAME
call :readenv CRONUS_DB_USER DBUSER
call :readenv CRONUS_DB_PASSWORD DBPASS
if "%DBHOST%"=="" set "DBHOST=127.0.0.1"
if "%DBPORT%"=="" set "DBPORT=3306"
if "%DBNAME%"=="" set "DBNAME=Cronus186"
if "%DBUSER%"=="" set "DBUSER=root"
if "%DBPASS%"=="" set "DBPASS=root"
set /p "DBHOST=  MySQL host     [%DBHOST%]: "
set /p "DBPORT=  MySQL port     [%DBPORT%]: "
set /p "DBUSER=  MySQL user     [%DBUSER%]: "
set /p "DBPASS=  MySQL password [%DBPASS%]: "
set /p "DBNAME=  database name  [%DBNAME%]: "
set "SETUP_DBHOST=%DBHOST%"
set "SETUP_DBPORT=%DBPORT%"
set "SETUP_DBNAME=%DBNAME%"
set "SETUP_DBUSER=%DBUSER%"
set "SETUP_DBPASS=%DBPASS%"
powershell -NoProfile -Command ^
  "$utf8 = New-Object System.Text.UTF8Encoding $false;" ^
  "$env_ = [System.IO.File]::ReadAllText('.env', $utf8);" ^
  "foreach ($pair in @(@('CRONUS_DB_HOST',$env:SETUP_DBHOST),@('CRONUS_DB_PORT',$env:SETUP_DBPORT),@('CRONUS_DB_NAME',$env:SETUP_DBNAME),@('CRONUS_DB_USER',$env:SETUP_DBUSER),@('CRONUS_DB_PASSWORD',$env:SETUP_DBPASS))) {" ^
  "  $k = $pair[0]; $v = $pair[1]; $line = $k + '=' + $v;" ^
  "  if ($env_ -match ('(?m)^#?' + $k + '=.*$')) { $env_ = $env_ -replace ('(?m)^#?' + $k + '=.*$'), $line } else { $env_ = $env_.TrimEnd() + \"`r`n$line\" } };" ^
  "[System.IO.File]::WriteAllText('.env', $env_.TrimEnd() + \"`r`n\", $utf8)"
if errorlevel 1 (
    echo [!] Could not update .env - edit it by hand: set CRONUS_DB_HOST / PORT / NAME / USER / PASSWORD.
    goto :halt
)
set "MYSQLEXE="
where mysql >nul 2>&1 && set "MYSQLEXE=mysql"
if "%MYSQLEXE%"=="" for /d %%d in ("%ProgramFiles%\MySQL\MySQL Server *") do if exist "%%~d\bin\mysql.exe" set "MYSQLEXE=%%~d\bin\mysql.exe"
if "%MYSQLEXE%"=="" (
    echo [4/6] MySQL settings recorded in .env ^(mysql.exe not found, so the connection was not tested^).
    goto :mysqldone
)
"%MYSQLEXE%" -h%DBHOST% -P%DBPORT% -u%DBUSER% -p%DBPASS% -e "SELECT 1" >nul 2>&1
if errorlevel 1 (
    echo [!] Could not connect to MySQL at %DBHOST%:%DBPORT% as %DBUSER%.
    echo     Start MySQL 8 and check the user/password, then run setup.bat again or edit .env.
    echo     ^(The server refuses to start without MySQL; CRONUS_DB=sqlite is the no-MySQL fallback.^)
) else (
    echo [4/6] MySQL connection OK ^(%DBHOST%:%DBPORT% as %DBUSER%, database %DBNAME%^).
)
:mysqldone

rem --- 5. build --------------------------------------------------------------
echo [5/6] Building the solution ^(first build downloads packages - a few minutes^)...
dotnet build Cronus.slnx -c Debug --nologo -v quiet
if errorlevel 1 (
    echo [!] Build failed. If the error mentions a locked file, a Cronus server is
    echo     still running - close it and run setup.bat again.
    goto :halt
)

rem --- 6. game data ----------------------------------------------------------
echo [6/6] Building gamedata.db from the client...
set "CRONUS_SETUP_CHAIN=1"
set "CRONUS_INGEST_NOBUILD=1"
call "%~dp0ingest.bat" "%CLIENT%"
if errorlevel 1 goto :halt
set "CRONUS_SETUP_CHAIN="
set "CRONUS_INGEST_NOBUILD="

echo.
echo ============================================
echo   Setup complete. Next steps:
echo ============================================
echo   1. run-server.bat           - start the server ^(local play works now^)
echo   2. port_open.bat            - open the firewall when friends join
echo      + forward the same ports on your router ^(docs\SERVER_SETUP.md Part 3^)
echo      + set CRONUS_HOST in .env to your public IP
echo   3. Each player: EmuClient pointed at your IP, WZ patch applied
echo      ^(docs\SERVER_SETUP.md Part 4^)

:halt
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
