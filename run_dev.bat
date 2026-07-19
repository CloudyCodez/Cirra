@echo off
setlocal

set PROJECT=src\CloudBerry.App\CloudBerry.App.csproj

echo.
echo ============================================================
echo  CloudBerry ^| NieR:Automata Theme Dev Run
echo  Crosshatch grid + 2B/A2 artwork active
echo ============================================================
echo.

dotnet run --project "%PROJECT%" --configuration Debug

if %ERRORLEVEL% neq 0 (
    echo.
    echo  Build/run failed. Check the output above.
    pause
    exit /b %ERRORLEVEL%
)

endlocal
