@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-local.ps1"
if errorlevel 1 (
    echo.
    echo BUILD FAILED - see errors above.
) else (
    echo.
    echo BUILD OK - output: ..\PCL Resurrection\PCL Resurrection.exe
)
pause
