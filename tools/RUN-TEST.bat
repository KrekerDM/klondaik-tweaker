@echo off
chcp 65001 >nul
title Klondaik Tweaker - test
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
cd /d "%~dp0"
echo.
echo [1/2] Read-only check of every module...
KlondaikTweaker.exe --selftest "%~dp0report-read.txt"
echo [2/2] Apply and revert round trip...
KlondaikTweaker.exe --selftest-apply "%~dp0report-apply.txt"
echo.
echo Done. Reports are next to this file.
notepad "%~dp0report-apply.txt"
