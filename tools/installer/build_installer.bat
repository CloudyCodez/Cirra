@echo off
setlocal EnableExtensions

for %%I in ("%~dp0..\..") do set ROOT=%%~fI
pushd "%ROOT%"

set PROJECT=src\CloudBerry.App\CloudBerry.App.csproj
set APP_VERSION=
set OUTPUT_BASE=setupCB
if not "%~1"=="" set OUTPUT_BASE=%~1
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "[xml]$project = Get-Content '%PROJECT%'; $version = @($project.Project.PropertyGroup.Version)[0]; if ([string]::IsNullOrWhiteSpace($version)) { '1.0.0' } else { $version }"`) do set APP_VERSION=%%V
if not defined APP_VERSION set APP_VERSION=1.0.0

echo.
echo ============================================================
echo  CloudBerry ^| Installer Build
echo ============================================================
echo  Version: %APP_VERSION%
echo.

call publish.bat --no-explorer
if errorlevel 1 (
    popd
    exit /b %errorlevel%
)

set ISCC=
for %%P in ("%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "C:\Program Files\Inno Setup 6\ISCC.exe") do (
    if exist %%~fP set ISCC=%%~fP
)

if not defined ISCC (
    for %%P in (ISCC.exe) do set ISCC=%%~$PATH:P
)

if not defined ISCC (
    echo.
    echo Inno Setup 6 was not found. CloudBerry publish output is ready, but the installer could not be compiled.
    echo Install Inno Setup 6, then rerun tools\installer\build_installer.bat
    echo.
    popd
    exit /b 1
)

echo Compiling installer with:
echo %ISCC%
echo.
"%ISCC%" "/DMyAppVersion=%APP_VERSION%" "/DMyAppOutputBase=%OUTPUT_BASE%" "tools\installer\CloudBerry.iss"
set EXITCODE=%ERRORLEVEL%

if %EXITCODE% neq 0 (
    echo.
    echo Installer build failed. Check the Inno Setup output above.
    popd
    exit /b %EXITCODE%
)

set INSTALLER_PATH=%ROOT%\publish\installer\%OUTPUT_BASE%.exe
if exist "%INSTALLER_PATH%" (
    if /I "%OUTPUT_BASE%"=="setupCB" (
        copy /Y "%INSTALLER_PATH%" "%ROOT%\setupCB.exe" >nul
        copy /Y "%INSTALLER_PATH%" "%ROOT%\publish\setupCB.exe" >nul
    )
    if not exist "%LOCALAPPDATA%\CloudBerry\updates" mkdir "%LOCALAPPDATA%\CloudBerry\updates" >nul 2>&1
    copy /Y "%INSTALLER_PATH%" "%LOCALAPPDATA%\CloudBerry\updates\%OUTPUT_BASE%.exe" >nul
)

echo.
echo Done! Installer output is in:
echo %ROOT%\publish\installer
if /I "%OUTPUT_BASE%"=="setupCB" (
    echo.
    echo Convenience copies:
    echo %ROOT%\setupCB.exe
    echo %ROOT%\publish\setupCB.exe
)
echo.

popd
endlocal
