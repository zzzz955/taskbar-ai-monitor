@echo off
setlocal
chcp 65001 >nul
echo Taskbar AI Monitor - Install or Update from Repository
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$OutputEncoding=[System.Text.Encoding]::UTF8; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; & '%~dp0scripts\apply.ps1' %*"
set "pipelineExit=%ERRORLEVEL%"
echo.
if not "%pipelineExit%"=="0" powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$OutputEncoding=[System.Text.Encoding]::UTF8; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Write-Host 'Update pipeline failed with exit code %pipelineExit%.' -ForegroundColor Red"
echo.
pause
exit /b %pipelineExit%
