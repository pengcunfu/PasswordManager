@echo off
chcp 65001 >nul
cd /d "%~dp0.."
title Credential Manager API
echo Starting API
echo   本机:     http://localhost:5080
powershell -NoProfile -Command "Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.)' } | ForEach-Object { Write-Host ('  局域网:   http://{0}:5080' -f $_.IPAddress) }"
echo.
dotnet run --project src\PasswordManager.Api
if errorlevel 1 pause
