@echo off
chcp 65001 >nul
title Klondaik Tweaker - open guest operations
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "SHARE=%~dp0"

echo.
echo This allows VMware guest operations to sign in with a blank password.
echo It lowers security and is meant for a throwaway test virtual machine only.
echo.

reg add "HKLM\SYSTEM\CurrentControlSet\Control\Lsa" /v LimitBlankPasswordUse /t REG_DWORD /d 0 /f >nul 2>&1
if errorlevel 1 (
    echo Could not write the policy. Run this file as administrator.
    pause
    exit /b 1
)

> "%SHARE%guest-info.txt" (
    echo user=%USERNAME%
    echo computer=%COMPUTERNAME%
    echo blankpassword=allowed
)

echo Done.
echo Account name: %USERNAME%
echo Written to the shared folder: guest-info.txt
echo.
echo You can close this window.
timeout /t 8 >nul
