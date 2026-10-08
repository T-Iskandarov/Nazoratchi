@echo off
chcp 65001 >nul
title Nazoratchi Xizmatini To'xtatish

:: Administrator huquqini tekshirish va so'rash
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Administrator huquqi so'ralmoqda...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo [1/2] NazoratchiService to'xtatilmoqda va o'chirilmoqda...
sc.exe stop NazoratchiService
timeout /t 2 /nobreak >nul
sc.exe delete NazoratchiService

echo [2/2] Tarmoq DNS sozlamalari avtomatik (DHCP) holatga qaytarilmoqda...
powershell -Command "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Set-DnsClientServerAddress -ResetServerAddresses"

echo.
echo Xizmat to'xtatildi va DNS tiklandi!
pause
