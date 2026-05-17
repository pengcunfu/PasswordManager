@echo off
setlocal
title Password Manager - Release Build
cd /d "%~dp0"

set "PROJECT=PasswordManager.csproj"
set "OUTPUT_DIR=bin\Release\net8.0-windows"
set "OUTPUT=%OUTPUT_DIR%\PasswordManager.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    echo Install .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Building Release (desktop / WinExe, no console)...
dotnet build "%PROJECT%" -c Release
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed
    pause
    exit /b 1
)

echo.
echo Build succeeded
echo Output: %~dp0%OUTPUT%
echo Mode:   WinExe (desktop application, no console window)
exit /b 0
