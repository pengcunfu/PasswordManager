@echo off
chcp 65001 >nul
cd /d "%~dp0..\src\PasswordManager.Web"
title Credential Manager Web
if not exist node_modules (
  echo Installing npm packages...
  call npm install
  if errorlevel 1 (
    pause
    exit /b 1
  )
)
echo Starting Web at http://localhost:5173
call npm run dev
if errorlevel 1 pause
