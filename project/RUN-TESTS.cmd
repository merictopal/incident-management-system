@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\test-all.ps1" %*
echo.
echo Finished. Results: test-logs\summary.txt
pause
