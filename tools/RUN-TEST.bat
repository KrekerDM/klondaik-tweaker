@echo off
chcp 65001 >nul
title Klondaik Tweaker - test
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
set "SRC=%~dp0"
set "WORK=C:\KLTest"
if not exist "%WORK%" mkdir "%WORK%"
copy /y "%SRC%KlondaikTweaker.exe" "%WORK%" >nul
cd /d "%WORK%"
echo.
echo [1/2] Read-only check of every module...
KlondaikTweaker.exe --selftest report-read.txt
echo [2/2] Apply and revert round trip...
KlondaikTweaker.exe --selftest-apply report-apply.txt
echo.
if exist report-apply.txt (
    copy /y report-read.txt "%SRC%" >nul 2>&1
    copy /y report-read.json "%SRC%" >nul 2>&1
    copy /y report-apply.txt "%SRC%" >nul 2>&1
    copy /y report-apply.json "%SRC%" >nul 2>&1
    echo Done. Reports copied to the shared folder.
    type report-apply.txt
    notepad report-apply.txt
) else (
    echo Report was not created. Check that the exe ran with administrator rights.
    pause
)
