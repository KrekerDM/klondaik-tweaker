@echo off
chcp 65001 >nul
title Klondaik Tweaker - full catalogue test
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
echo Every tweak in the catalogue will be applied, checked and reverted.
echo This takes a while. The report is written after each tweak, so an
echo interruption does not lose what already ran.
echo.
KlondaikTweaker.exe --selftest-apply report-all.txt --all
echo.
if exist report-all.txt (
    copy /y report-all.txt "%SRC%" >nul 2>&1
    copy /y report-all.json "%SRC%" >nul 2>&1
    echo Done. Report copied to the shared folder.
    notepad report-all.txt
) else (
    echo Report was not created. Check that the exe ran with administrator rights.
    pause
)
