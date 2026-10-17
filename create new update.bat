@echo off
setlocal EnableExtensions EnableDelayedExpansion

for %%I in ("%~dp0.") do set ROOT=%%~fI
pushd "%ROOT%" >nul

echo.
echo ==============================================================
echo   Cirra ^| Update Builder
echo ==============================================================
echo.

:: ── Read current version from csproj (safe fallback if PowerShell fails) ──────
set PROJECT=src\CloudBerry.App\CloudBerry.App.csproj
set CURRENT_VERSION=unknown
if exist "%ROOT%\%PROJECT%" (
    for /f "usebackq tokens=*" %%V in (`powershell -NoProfile -NonInteractive -Command "$f='%ROOT%\%PROJECT%'; try { [xml]$p=Get-Content $f; $v=@($p.Project.PropertyGroup.Version)[0]; if($v){ $v } else { 'unknown' } } catch { 'unknown' }" 2^>nul`) do set CURRENT_VERSION=%%V
)

echo   Current version: %CURRENT_VERSION%
echo.
echo   What do you want to name this update package?
echo   Examples:  1.2.3  ^|  2-5  ^|  hotfix1
echo   Output will be:  update^<name^>.exe
echo.
set UPDATE_ID=
set /p "UPDATE_ID=  Name: "
echo.

if not defined UPDATE_ID (
    echo   [!] No name entered. Nothing built.
    echo.
    goto :done_ok
)

:: Strip spaces
set OUTPUT_BASE=%UPDATE_ID: =%

:: Strip leading "update" prefix if user typed it
set STRIPPED=!OUTPUT_BASE!
if /I "!STRIPPED:~0,6!"=="update" set STRIPPED=!STRIPPED:~6!
if not defined STRIPPED (
    echo   [X] The name was empty after removing the "update" prefix. Nothing built.
    echo.
    goto :done_fail
)

set PACKAGE_NAME=update!STRIPPED!
set FINAL_EXE=%ROOT%\!PACKAGE_NAME!.exe

echo   Building  !PACKAGE_NAME!.exe  ...
echo   --------------------------------------------------------------
echo.

call "%ROOT%\tools\installer\build_installer.bat" "!PACKAGE_NAME!"
set BUILD_EXIT=%ERRORLEVEL%

echo.
echo   --------------------------------------------------------------

if %BUILD_EXIT% neq 0 (
    echo.
    echo   [X] BUILD FAILED  ^(exit code %BUILD_EXIT%^)
    echo.
    echo       What to check:
    echo         - Look above for lines starting with "error CS"
    echo         - Make sure Inno Setup 6 is installed
    echo         - Make sure dotnet publish succeeded
    echo.
    goto :done_fail
)

:: ── Copy output to root ───────────────────────────────────────────────────────
set SOURCE_EXE=%ROOT%\publish\installer\!PACKAGE_NAME!.exe
if not exist "!SOURCE_EXE!" (
    echo.
    echo   [X] Build finished but the output file was not found:
    echo         !SOURCE_EXE!
    echo.
    echo       Check publish\installer\ for the file manually.
    echo.
    goto :done_fail
)

copy /Y "!SOURCE_EXE!" "!FINAL_EXE!" >nul
if errorlevel 1 (
    echo.
    echo   [X] Could not copy the installer to the project root.
    echo         From: !SOURCE_EXE!
    echo         To:   !FINAL_EXE!
    echo.
    goto :done_fail
)

:: ── Success ───────────────────────────────────────────────────────────────────
echo.
echo   ==============================================================
echo     BUILD COMPLETE
echo   ==============================================================
echo.
echo     Package : !PACKAGE_NAME!.exe
echo     Version : %CURRENT_VERSION%
echo     Saved to: !FINAL_EXE!
echo.
echo   Also staged to:
echo     %LOCALAPPDATA%\Cirra\updates\!PACKAGE_NAME!.exe
echo.

:: ── Run prompt ───────────────────────────────────────────────────────────────
set RUN_CHOICE=
set /p "RUN_CHOICE=  Run !PACKAGE_NAME!.exe now? [Y/N]: "
echo.
if /I "!RUN_CHOICE!"=="Y" (
    echo   Launching !PACKAGE_NAME!.exe ...
    echo.
    start "" "!FINAL_EXE!"
) else (
    echo   Skipped. You can run it later from:
    echo     !FINAL_EXE!
    echo.
)

:done_ok
pause
popd >nul
endlocal
exit /b 0

:done_fail
pause
popd >nul
endlocal
exit /b 1
