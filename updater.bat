@echo off
setlocal enabledelayedexpansion

title CutCraft Auto-Updater
color 0A

echo ========================================================
echo               CutCraft Auto-Updater
echo ========================================================
echo.

set "APP_DIR=%~dp0"
set "EXE_NAME=CutCraft.exe"
set "UPDATE_PACKAGE=%~1"
set "TARGET_PID=%~2"
set "DO_RESTART=%~3"

if "%UPDATE_PACKAGE%"=="" (
    set "UPDATE_PACKAGE=%APP_DIR%CutCraft-Update.zip"
)
if "%DO_RESTART%"=="" (
    set "DO_RESTART=1"
)

echo [1/3] Waiting for CutCraft to close...
:waitloop
if not "%TARGET_PID%"=="" (
    tasklist /fi "PID eq %TARGET_PID%" 2>nul | find "%TARGET_PID%" >nul
    if !ERRORLEVEL! EQU 0 (
        timeout /t 1 /nobreak >nul
        goto waitloop
    )
) else (
    timeout /t 2 /nobreak >nul
)

if exist "%UPDATE_PACKAGE%" (
    echo [2/3] Extracting update package: %UPDATE_PACKAGE%...
    tar -xf "%UPDATE_PACKAGE%" -C "%APP_DIR%"
    if !ERRORLEVEL! NEQ 0 (
        echo [INFO] Falling back to PowerShell Expand-Archive...
        powershell -NoProfile -Command "Expand-Archive -Path '%UPDATE_PACKAGE%' -DestinationPath '%APP_DIR%' -Force"
    )
    if !ERRORLEVEL! EQU 0 (
        echo [SUCCESS] Package extracted successfully.
        del "%UPDATE_PACKAGE%" >nul 2>&1
    ) else (
        echo [ERROR] Extraction failed.
    )
) else (
    echo [INFO] No update package zip found at: %UPDATE_PACKAGE%
)

echo [3/3] Checking relaunch...
if "%DO_RESTART%"=="1" (
    if exist "%APP_DIR%%EXE_NAME%" (
        echo Relaunching CutCraft...
        start "" "%APP_DIR%%EXE_NAME%"
    )
)

echo ========================================================
echo CutCraft update process finished!
echo ========================================================
exit /b 0
