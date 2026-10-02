@echo off
chcp 65001 >nul
if not exist "%~dp0Install-XIII-VR.ps1" (
  echo Unpack the whole archive first ^(right-click the zip - Extract All^), then run this file from the unpacked folder.
  echo.
  pause
  exit /b 1
)
powershell.exe -NoProfile -File "%~dp0Install-XIII-VR.ps1" %*
set "XIII_VR_RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %XIII_VR_RESULT%
