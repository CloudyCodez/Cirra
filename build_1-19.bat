@echo off
setlocal EnableExtensions

:: ============================================================
::  CloudBerry  |  v1-19 Installer Builder
::  Builds CloudBerry.exe, compiles the Inno Setup installer,
::  and saves it as:
::    <root>\update1-19.exe            (project root copy)
::    <root>\publish\installer\update1-19.exe
::    %LOCALAPPDATA%\CloudBerry\updates\update1-19.exe
:: ============================================================

for %%I in ("%~dp0.") do set ROOT=%%~fI

set PROJECT=src\CloudBerry.App\CloudBerry.App.csproj
set OUTPUT_NAME=update1-19
set PUBLISH_DIR=%ROOT%\publish\win-x64
set INSTALLER_DIR=%ROOT%\publish\installer

:: ── Resolve app version from the .csproj ─────────────────────────────────────
set APP_VERSION=
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command ^
    "[xml]$p = Get-Content '%ROOT%\%PROJECT%'; $v = @($p.Project.PropertyGroup.Version)[0]; if ([string]::IsNullOrWhiteSpace($v)) { '1.19.0' } else { $v }"`) do set APP_VERSION=%%V
if not defined APP_VERSION set APP_VERSION=1.19.0

echo.
echo ============================================================
echo  CloudBerry  ^|  Installer Build  ^|  %OUTPUT_NAME%
echo ============================================================
echo  Version   : %APP_VERSION%
echo  Output    : %ROOT%\%OUTPUT_NAME%.exe
echo.

:: ── Step 1: dotnet publish ────────────────────────────────────────────────────
pushd "%ROOT%"

echo [1/3] Publishing self-contained single-file...
dotnet publish "%PROJECT%" ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=true ^
  /p:DebugType=embedded ^
  --output "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo [ERROR] dotnet publish failed. See above for details.
    popd
    pause
    exit /b 1
)
echo  Done.
echo.

:: ── Step 2: locate Inno Setup ────────────────────────────────────────────────
set ISCC=
for %%P in (
    "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    "C:\Program Files\Inno Setup 6\ISCC.exe"
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) do (
    if exist %%~fP (
        if not defined ISCC set ISCC=%%~fP
    )
)

if not defined ISCC (
    for %%P in (ISCC.exe) do set ISCC=%%~$PATH:P
)

if not defined ISCC (
    echo [ERROR] Inno Setup 6 was not found.
    echo         Install from https://jrsoftware.org/isdl.php then rerun this script.
    popd
    pause
    exit /b 1
)

:: ── Step 3: compile installer ─────────────────────────────────────────────────
echo [2/3] Compiling installer...
echo  ISCC: %ISCC%
echo.

"%ISCC%" ^
  "/DMyAppVersion=%APP_VERSION%" ^
  "/DMyAppOutputBase=%OUTPUT_NAME%" ^
  "tools\installer\CloudBerry.iss"

if errorlevel 1 (
    echo.
    echo [ERROR] Inno Setup compile failed. See above for details.
    popd
    pause
    exit /b 1
)
echo  Done.
echo.

:: ── Step 4: copy to destinations ─────────────────────────────────────────────
echo [3/3] Distributing installer...

set INSTALLER_SRC=%INSTALLER_DIR%\%OUTPUT_NAME%.exe

if not exist "%INSTALLER_SRC%" (
    echo [ERROR] Expected installer not found at:
    echo         %INSTALLER_SRC%
    popd
    pause
    exit /b 1
)

:: Root convenience copy
copy /Y "%INSTALLER_SRC%" "%ROOT%\%OUTPUT_NAME%.exe" >nul
echo  Copied to: %ROOT%\%OUTPUT_NAME%.exe

:: Local updates cache
if not exist "%LOCALAPPDATA%\CloudBerry\updates" (
    mkdir "%LOCALAPPDATA%\CloudBerry\updates" >nul 2>&1
)
copy /Y "%INSTALLER_SRC%" "%LOCALAPPDATA%\CloudBerry\updates\%OUTPUT_NAME%.exe" >nul
echo  Cached at:  %LOCALAPPDATA%\CloudBerry\updates\%OUTPUT_NAME%.exe

echo.
echo ============================================================
echo  Build complete!
echo.
echo  update1-19.exe
echo    %ROOT%\%OUTPUT_NAME%.exe
echo    %INSTALLER_DIR%\%OUTPUT_NAME%.exe
echo    %LOCALAPPDATA%\CloudBerry\updates\%OUTPUT_NAME%.exe
echo ============================================================
echo.

popd
endlocal
