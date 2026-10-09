@echo off
chcp 65001 >nul
title Nazoratchi - Installer Yaratish (Build Setup)
echo =========================================================================
echo       NAZORATCHI - YAGONA SETUP O'RNATUVCHISINI YARATISH
echo =========================================================================
echo.

set "PATH=%LOCALAPPDATA%\dotnet;%PATH%"

set "ISCC=C:\Users\CUBO\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
    echo [XATO] Inno Setup 6 kompilyatori topilmadi!
    echo Iltimos, Inno Setup 6 o'rnatilganligini tekshiring.
    pause
    exit /b
)

echo [1/3] Nazoratchi.Service mustaqil (self-contained) rejimda publish qilinmoqda...
dotnet publish "%~dp0src\Nazoratchi.Service\Nazoratchi.Service.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0publish\service"
if %errorLevel% neq 0 (
    echo [XATO] Service publish qilishda xatolik yuz berdi!
    pause
    exit /b
)

echo [2/3] Nazoratchi.Panel mustaqil (self-contained) rejimda publish qilinmoqda...
dotnet publish "%~dp0src\Nazoratchi.Panel\Nazoratchi.Panel.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0publish\panel"
if %errorLevel% neq 0 (
    echo [XATO] Panel publish qilishda xatolik yuz berdi!
    pause
    exit /b
)

echo [3/3] Inno Setup orqali Nazoratchi_Setup_v1.0.exe yig'ilmoqda...
"%ISCC%" "%~dp0installer\installer.iss"
if %errorLevel% neq 0 (
    echo [XATO] Installer yig'ishda xatolik yuz berdi!
    pause
    exit /b
)

echo.
echo =========================================================================
echo [MUVAFFAQIYATLI] O'rnatuvchi dastur tayyor:
echo %~dp0dist\Nazoratchi_Setup_v1.0.exe
echo =========================================================================
echo.
pause
