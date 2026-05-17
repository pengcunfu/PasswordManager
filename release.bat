@echo off
setlocal
title Password Manager - Release Build
cd /d "%~dp0"

set "PROJECT=PasswordManager.csproj"
set "OUTPUT=bin\Release\net8.0-windows\PasswordManager.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    echo Install .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo ============================================================
echo Building Release configuration
echo ============================================================
dotnet build "%PROJECT%" -c Release
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed
    pause
    exit /b 1
)

echo.
echo ============================================================
echo Build succeeded
echo ============================================================
echo Output directory: %~dp0bin\Release\net8.0-windows
echo Executable: %OUTPUT%
echo ============================================================
pause
exit /b 0
