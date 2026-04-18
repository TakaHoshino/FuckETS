@echo off
chcp 65001 >nul
setlocal

python --version >nul 2>&1
if errorlevel 1 (
    pause
    exit /b 1
)

pip install -q reportlab pyinstaller >nul 2>&1
if errorlevel 1 (
    pause
    exit /b 1
)

pyinstaller --noconfirm --windowed --name "FuckETS" main.py >nul 2>&1
if errorlevel 1 (
    pause
    exit /b 1
)

if exist "build"           rmdir /s /q "build"
if exist "FuckETS.spec" del /q "FuckETS.spec"

explorer dist
endlocal