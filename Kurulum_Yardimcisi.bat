@echo off
chcp 65001 >nul
title OpenWidget Kurulum Yardimcisi
cd /d "%~dp0"
echo OpenWidget Kurulum Yardimcisi baslatiliyor...
start "" "%~dp0OpenWidget.exe" --setup
