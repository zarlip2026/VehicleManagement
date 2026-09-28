@echo off
setlocal
title Vehicle Management - Install Prerequisites
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
set "result=%errorlevel%"
echo.
pause
exit /b %result%
