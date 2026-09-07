@echo off
rem Cronus wz tool — inspect / edit the client's .wz archives (see docs/WZ_TOOLING.md).
rem   wz info <file.wz> | ls | dump | png | rewrite | graft | verify
setlocal
dotnet run -c Release --project "%~dp0WzTool\Cronus.WzTool.csproj" -- %*
exit /b %ERRORLEVEL%
