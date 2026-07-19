@echo off
setlocal EnableDelayedExpansion

set CSPROJ=src\CloudBerry.App\CloudBerry.App.csproj

:MENU
cls
echo.
echo ============================================================
echo  CloudBerry ^| Push to GitHub
echo ============================================================
echo.
call :SHOWSTATUS
echo.
echo   1. First-time setup ^(set remote URL^)
echo   2. Commit changes
echo   3. Commit + tag + push a release
echo   4. Push current branch
echo   5. Pull latest
echo   6. Show status / diff summary
echo   7. Untrack files that should now be ignored
echo   8. Exit
echo.
set /p CHOICE="  Pick an option (1-8): "

if "%CHOICE%"=="1" goto SETUP
if "%CHOICE%"=="2" goto COMMIT
if "%CHOICE%"=="3" goto RELEASE
if "%CHOICE%"=="4" goto PUSH
if "%CHOICE%"=="5" goto PULL
if "%CHOICE%"=="6" goto STATUS
if "%CHOICE%"=="7" goto UNTRACK
if "%CHOICE%"=="8" goto END
echo.
echo   Not a valid option.
pause
goto MENU

:SHOWSTATUS
git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo   Not a git repo yet - run option 1 first.
    goto :eof
)
for /f "delims=" %%b in ('git branch --show-current 2^>nul') do set BRANCH=%%b
for /f "delims=" %%r in ('git remote get-url origin 2^>nul') do set REMOTEURL=%%r
if "%BRANCH%"=="" set BRANCH=(no branch yet)
if "%REMOTEURL%"=="" set REMOTEURL=(no remote set - run option 1)
echo   Branch: %BRANCH%
echo   Remote: %REMOTEURL%
goto :eof

:SETUP
cls
echo.
echo ============================================================
echo  First-time setup
echo ============================================================
echo.
git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo   No git repo found here yet. Initializing...
    git init
    git branch -M main
)
echo.
echo   Paste your GitHub repo URL below.
echo   Example: https://github.com/yourname/CloudBerry.git
echo.
set /p REPOURL="  Repo URL: "
if "%REPOURL%"=="" (
    echo   No URL entered - cancelled.
    pause
    goto MENU
)
git remote get-url origin >nul 2>&1
if errorlevel 1 (
    git remote add origin "%REPOURL%"
) else (
    git remote set-url origin "%REPOURL%"
)
echo.
echo   Remote set. First push will need option 4 (or option 3 for a
echo   tagged release) once you've made a commit.
pause
goto MENU

:COMMIT
cls
echo.
echo ============================================================
echo  Commit changes
echo ============================================================
echo.
git status --short
echo.
set /p MSG="  Commit message: "
if "%MSG%"=="" (
    echo   No message entered - cancelled.
    pause
    goto MENU
)
git add -A
git commit -m "%MSG%"
echo.
echo   Committed. Use option 4 to push whenever you're ready.
pause
goto MENU

:RELEASE
cls
echo.
echo ============================================================
echo  Commit + tag + push a release
echo ============================================================
echo.

rem -- pull current version out of the App csproj --
set CURVERSION=
for /f "tokens=2 delims=><" %%a in ('findstr /r "<Version>" "%CSPROJ%"') do set CURVERSION=%%a
echo   Current version in csproj: %CURVERSION%
echo.
set /p NEWVERSION="  New version (blank = keep %CURVERSION%): "
if "%NEWVERSION%"=="" set NEWVERSION=%CURVERSION%

if not "%NEWVERSION%"=="%CURVERSION%" (
    echo.
    echo   Updating csproj version to %NEWVERSION%...
    powershell -NoProfile -Command "(Get-Content '%CSPROJ%') -replace '<Version>.*</Version>', '<Version>%NEWVERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%NEWVERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%NEWVERSION%.0</FileVersion>' -replace '<InformationalVersion>.*</InformationalVersion>', '<InformationalVersion>%NEWVERSION%</InformationalVersion>' | Set-Content '%CSPROJ%'"
)

echo.
git status --short
echo.
set /p MSG="  Commit message (blank = 'Release v%NEWVERSION%'): "
if "%MSG%"=="" set MSG=Release v%NEWVERSION%

git add -A
git commit -m "%MSG%"

echo.
set TAGNAME=v%NEWVERSION%
echo   Tagging as %TAGNAME%...
git tag -a %TAGNAME% -m "%MSG%"

echo.
echo   Pushing branch and tag...
git push origin HEAD
git push origin %TAGNAME%

echo.
echo   Done. %TAGNAME% is pushed - create the GitHub Release from that
echo   tag on github.com if you want release notes / attached binaries.
pause
goto MENU

:PUSH
cls
echo.
echo ============================================================
echo  Push current branch
echo ============================================================
echo.
git push origin HEAD
echo.
pause
goto MENU

:PULL
cls
echo.
echo ============================================================
echo  Pull latest
echo ============================================================
echo.
git pull
echo.
pause
goto MENU

:STATUS
cls
echo.
echo ============================================================
echo  Status
echo ============================================================
echo.
call :SHOWSTATUS
echo.
git status
echo.
echo   -- recent commits --
git log --oneline -10
echo.
pause
goto MENU

:UNTRACK
cls
echo.
echo ============================================================
echo  Untrack files that should now be ignored
echo ============================================================
echo.
echo   This clears git's index and re-adds everything using the
echo   current .gitignore. Useful once, right after tightening
echo   .gitignore, to drop already-committed bin/obj/publish/exe
echo   files without deleting them from disk.
echo.
echo   Nothing on disk is touched - this only changes what git tracks.
echo   You'll still need to commit the result afterward (option 2).
echo.
set /p CONFIRM="  Continue? (y/n): "
if /I not "%CONFIRM%"=="y" goto MENU
git rm -r --cached . >nul
git add -A
echo.
echo   Re-indexed. Run "git status" (option 6) to see what dropped
echo   out, then commit with option 2 when it looks right.
pause
goto MENU

:END
endlocal
exit /b 0
