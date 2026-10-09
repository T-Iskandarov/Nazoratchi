@echo off
chcp 65001 >nul
title Nazoratchi - Boshqaruv Paneli
echo Nazoratchi Boshqaruv Paneli ishga tushirilmoqda...

set "PANEL_EXE=%~dp0src\Nazoratchi.Panel\bin\Debug\net8.0-windows\Nazoratchi.Panel.exe"
if not exist "%PANEL_EXE%" set "PANEL_EXE=%~dp0src\Nazoratchi.Panel\bin\Release\net8.0-windows\Nazoratchi.Panel.exe"

if not exist "%PANEL_EXE%" (
    echo.
    echo [XATO] Panel dasturi topilmadi!
    echo Iltimos, avval dasturni build qiling: dotnet build Nazoratchi.sln
    pause
    exit /b
)

start "" "%PANEL_EXE%"
exit
