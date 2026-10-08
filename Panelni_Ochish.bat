@echo off
chcp 65001 >nul
title Nazoratchi - Boshqaruv Paneli
echo Nazoratchi Boshqaruv Paneli ishga tushirilmoqda...
start "" "%~dp0src\Nazoratchi.Panel\bin\Debug\net8.0-windows\Nazoratchi.Panel.exe"
exit
