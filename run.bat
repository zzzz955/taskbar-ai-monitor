@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

set "INSTALLED_EXE=%LOCALAPPDATA%\TaskbarAiMonitor\bin\TaskbarAiMonitor.exe"
if exist "%INSTALLED_EXE%" (
    start "" "%INSTALLED_EXE%"
    exit /b 0
)

if exist "bin\Release\net9.0-windows\TaskbarAiMonitor.exe" (
    start "" "bin\Release\net9.0-windows\TaskbarAiMonitor.exe"
    exit /b 0
)

echo Building and starting Taskbar AI Monitor...
dotnet run
