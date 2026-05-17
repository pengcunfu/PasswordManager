@echo off
title Password Manager - Debug
cd /d "%~dp0"

set "PROJECT=PasswordManager.csproj"
set "EXE=bin\Debug\net8.0-windows\PasswordManager.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    echo Install .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Building Debug (console mode)...
dotnet build "%PROJECT%" -c Debug
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed
    pause
    exit /b 1
)

echo Starting Password Manager (debug, console attached)...
echo.
"%EXE%"
if errorlevel 1 (
    echo.
    echo [ERROR] Application exited with code %errorlevel%
    pause
)
