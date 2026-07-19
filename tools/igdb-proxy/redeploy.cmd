@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0redeploy.ps1" %*
echo.
echo Press any key to close this setup window.
pause >nul
endlocal
