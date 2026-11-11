@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-local.ps1" %*
echo.
echo Running. Open http://localhost:5173  -  stop with STOP-LOCAL.cmd
pause
