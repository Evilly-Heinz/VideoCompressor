@echo off
net session >nul 2>&1
if %errorlevel% neq 0 (echo Run as Administrator! & pause & exit /b 1)

echo Removing VideoCompressor context menu entries...
for %%E in (mp4 mov avi mkv wmv flv webm) do (
    reg delete "HKEY_CLASSES_ROOT\SystemFileAssociations\.%%E\shell\VideoCompressor" /f >nul 2>&1
    echo   Removed .%%E
)
echo Done.
pause
