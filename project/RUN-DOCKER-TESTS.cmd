@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\test-docker.ps1" %*
echo.
echo Finished. Results: test-logs\docker-summary.txt
pause
