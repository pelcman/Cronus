@echo off
rem Grafts the Balrog-ship image (Map.wz/Obj/vehicle.img/ship/ossyria/97) from another version's
rem Map.wz into the client's Map.wz. The JMS v186 data ships a 1x1 placeholder there, so the
rem airship raid draws no enemy ship until this is done. See docs/WZ_TOOLING.md.
rem
rem   wz_graft_airship.bat <source Map.wz> [<client folder>]
setlocal
set "SRC=%~1"
set "CLIENT=%~2"
if "%SRC%"=="" (
  echo usage: %~nx0 ^<source Map.wz^> [^<client folder^>]
  exit /b 1
)
if "%CLIENT%"=="" set "CLIENT=%~dp0..\..\Client\MapleStory_v186_edit"
set "DST=%CLIENT%\Map.wz"
set "NODE=Obj/vehicle.img/ship/ossyria/97"
if not exist "%SRC%" ( echo source not found: %SRC% & exit /b 1 )
if not exist "%DST%" ( echo client Map.wz not found: %DST% & exit /b 1 )

echo == source ship image
call "%~dp0wz.bat" ls "%SRC%" %NODE% || exit /b 1
call "%~dp0wz.bat" ls "%SRC%" %NODE% | findstr /C:"canvas 1x1" >nul && (
  echo the source's 97 is a 1x1 placeholder too - nothing to graft.
  exit /b 2
)

echo == grafting into %DST%.new
call "%~dp0wz.bat" graft "%SRC%" %NODE% "%DST%" %NODE% --out "%DST%.new" || exit /b 1

if not exist "%DST%.bak" (
  echo == backing up %DST% to %DST%.bak
  copy /y "%DST%" "%DST%.bak" >nul || exit /b 1
)
echo == replacing %DST%
move /y "%DST%.new" "%DST%" >nul || exit /b 1
echo done. Start the client and ride the airship; the .bak restores the original if anything looks wrong.
exit /b 0
