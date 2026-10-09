@echo off
chcp 65001 >nul
title Nazoratchi Xizmatini O'rnatish va Yoqish

:: Administrator huquqini tekshirish va so'rash
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Administrator huquqi so'ralmoqda...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "SERVICE_EXE=%~dp0src\Nazoratchi.Service\bin\Debug\net8.0-windows\win-x64\Nazoratchi.Service.exe"
if not exist "%SERVICE_EXE%" set "SERVICE_EXE=%~dp0src\Nazoratchi.Service\bin\Release\net8.0-windows\win-x64\Nazoratchi.Service.exe"
if not exist "%SERVICE_EXE%" set "SERVICE_EXE=%~dp0src\Nazoratchi.Service\bin\Debug\net8.0-windows\Nazoratchi.Service.exe"
if not exist "%SERVICE_EXE%" set "SERVICE_EXE=%~dp0src\Nazoratchi.Service\bin\Release\net8.0-windows\Nazoratchi.Service.exe"

if not exist "%SERVICE_EXE%" (
    echo.
    echo [XATO] Xizmat dasturi topilmadi!
    echo Iltimos, avval dasturni build qiling: dotnet build Nazoratchi.sln
    pause
    exit /b
)

echo [1/2] NazoratchiService xizmati ro'yxatdan o'tkazilmoqda...
sc.exe stop NazoratchiService >nul 2>&1
sc.exe delete NazoratchiService >nul 2>&1
timeout /t 1 /nobreak >nul
sc.exe create NazoratchiService binPath= "\"%SERVICE_EXE%\"" start= auto DisplayName= "Nazoratchi Xavfsizlik Xizmati"

echo [2/2] NazoratchiService ishga tushirilmoqda...
sc.exe start NazoratchiService

echo.
echo Xizmat muvaffaqiyatli ishga tushdi!
pause
