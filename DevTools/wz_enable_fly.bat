@echo off
rem Marks every map as a fly map (Map.wz: <map>.img/info/fly = 1) in the client's Map.wz, so the
rem Flying temporary stat (/gmfly) lets a GM fly anywhere instead of only on the 72 maps Nexon
rem flagged (天空地域, 母船, the Temple of Time flight). Players without the stat walk as before.
rem See docs/WZ_TOOLING.md.
rem
rem   wz_enable_fly.bat [<client folder>]      (default: Client\MapleStory_v186, the one the shortcut launches)
setlocal
set "CLIENT=%~1"
if "%CLIENT%"=="" set "CLIENT=%~dp0..\..\Client\MapleStory_v186"
set "DST=%CLIENT%\Map.wz"
if not exist "%DST%" ( echo client Map.wz not found: %DST% & exit /b 1 )

echo == writing %DST%.new with info/fly = 1 on every map (then verifying every image)
call "%~dp0wz.bat" set-int "%DST%" "Map/Map*/*.img" info/fly 1 --out "%DST%.new" || exit /b 1

if not exist "%DST%.bak" (
  echo == backing up %DST% to %DST%.bak
  copy /y "%DST%" "%DST%.bak" >nul || exit /b 1
)
echo == replacing %DST%
move /y "%DST%.new" "%DST%" >nul || exit /b 1
echo done. Restart the client; /gmfly should now fly on any map. Restore Map.wz.bak to undo.
exit /b 0
