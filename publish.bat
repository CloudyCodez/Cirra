@echo off
setlocal

set PROJECT=src\CloudBerry.App\CloudBerry.App.csproj
set OUTPUT=%~dp0publish\win-x64
set OPEN_OUTPUT=1

if /I "%~1"=="--no-explorer" (
    set OPEN_OUTPUT=0
)

echo.
echo ============================================================
echo  CloudBerry ^| Self-contained single-file publish
echo ============================================================
echo.

dotnet publish "%PROJECT%" ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=true ^
  /p:DebugType=embedded ^
  --output "%OUTPUT%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo  Build failed. Check the output above for errors.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo  Done!  CloudBerry.exe is in:
echo  %OUTPUT%
echo.
if "%OPEN_OUTPUT%"=="1" (
    explorer "%OUTPUT%"
)

endlocal
