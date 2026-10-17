@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0HostBoss\play.ps1" -Mode Normal
if errorlevel 1 pause
