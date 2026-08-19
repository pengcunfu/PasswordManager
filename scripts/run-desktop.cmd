@echo off
chcp 65001 >nul
cd /d "%~dp0.."
title Credential Manager Desktop
echo Starting desktop client...
echo Connect to http://localhost:8890 when prompted.
dotnet run --project src\PasswordManager.Desktop
if errorlevel 1 pause
