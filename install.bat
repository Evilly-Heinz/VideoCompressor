@echo off
:: ============================================================
::  VideoCompressor — Install context menu
::  Must run as Administrator.
::  Expects both VideoCompressorUI.exe and VideoCompressor.exe
::  to be in the SAME folder as this script.
:: ============================================================
setlocal EnableDelayedExpansion

echo.
echo  VideoCompressor — Context Menu Installer
echo  ==========================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] Please run as Administrator.
    echo  Right-click install.bat ^> "Run as administrator"
    pause & exit /b 1
)

set "INSTALL_DIR=%~dp0"
if "%INSTALL_DIR:~-1%"=="\" set "INSTALL_DIR=%INSTALL_DIR:~0,-1%"
echo  Install dir: %INSTALL_DIR%

if not exist "%INSTALL_DIR%\VideoCompressorUI.exe" (
    echo  [ERROR] VideoCompressorUI.exe not found.
    echo  Build the solution in Release^|x64 first, then copy bin\Release\ files here.
    pause & exit /b 1
)
if not exist "%INSTALL_DIR%\VideoCompressor.exe" (
    echo  [ERROR] VideoCompressor.exe not found.
    pause & exit /b 1
)
if not exist "%INSTALL_DIR%\ffmpeg.exe" (
    echo  [WARN]  ffmpeg.exe not found — compression will not work.
    echo  Download from https://github.com/BtbN/FFmpeg-Builds/releases
    echo  and place ffmpeg.exe in %INSTALL_DIR%
    echo.
    choice /c YN /m "Continue anyway?"
    if !errorlevel!==2 exit /b 1
)

set "REG_SRC=%~dp0scripts\install_context_menu.reg"
set "REG_TMP=%TEMP%\VC_ctx_menu.reg"

:: Escape backslashes for .reg format
set "ESC=%INSTALL_DIR:\=\\%"

powershell -NoProfile -Command ^
  "(Get-Content '%REG_SRC%') -replace '%%INSTALL_DIR%%','%ESC%' | Set-Content '%REG_TMP%'"

if %errorlevel% neq 0 (
    echo  [ERROR] Failed to patch registry template.
    pause & exit /b 1
)

reg import "%REG_TMP%"
if %errorlevel% neq 0 (
    echo  [ERROR] reg import failed.
    del "%REG_TMP%" >nul 2>&1
    pause & exit /b 1
)
del "%REG_TMP%" >nul 2>&1

echo.
echo  [OK] Context menu installed!
echo  Right-click any video file and choose "Compress this video".
echo.
pause
