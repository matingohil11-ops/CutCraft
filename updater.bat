@echo off
setlocal enabledelayedexpansion

title CutCraft Auto-Updater
color 0A

echo ========================================================
echo               CutCraft Auto-Updater v0.0
echo ========================================================
echo.

set "APP_DIR=%~dp0"
set "EXE_NAME=CutCraft.exe"
set "UPDATE_PACKAGE=%~1"

if "%UPDATE_PACKAGE%"=="" (
    set "UPDATE_PACKAGE=%APP_DIR%CutCraft-Update.zip"
)

echo [1/3] Waiting for CutCraft to close...
timeout /t 2 /nobreak >nul

if exist "%UPDATE_PACKAGE%" (
    echo [2/3] Extracting update package: %UPDATE_PACKAGE%...
    tar -xf "%UPDATE_PACKAGE%" -C "%APP_DIR%"
    if !ERRORLEVEL! EQU 0 (
        echo [SUCCESS] Package extracted successfully.
        del "%UPDATE_PACKAGE%" >nul 2>&1
    ) else (
        echo [WARNING] Extraction failed or tar not supported.
    )
) else (
    echo [INFO] No update package zip found; verifying latest installation.
)

echo [3/3] Relaunching CutCraft...
if exist "%APP_DIR%%EXE_NAME%" (
    start "" "%APP_DIR%%EXE_NAME%"
)

echo ========================================================
echo CutCraft update complete!
echo ========================================================
exit /b 0
