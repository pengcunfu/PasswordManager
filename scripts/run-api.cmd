@echo off
chcp 65001 >nul
cd /d "%~dp0.."
title Credential Manager API
echo Starting API at http://localhost:5080
dotnet run --project src\PasswordManager.Api
if errorlevel 1 pause
