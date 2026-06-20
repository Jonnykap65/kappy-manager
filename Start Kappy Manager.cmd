@echo off
setlocal
set "APP=%~dp0Kappy Manager.exe"
if not exist "%APP%" (
  echo Kappy Manager has not been built. Building it now...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
  )
)
start "" "%APP%"
