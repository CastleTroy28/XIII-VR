@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-XIII-Logs.ps1" -GameFolder "%~dp0."
pause
