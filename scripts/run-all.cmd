@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Starting API, Web, then Desktop...
start "Credential Manager API" cmd /k "%~dp0run-api.cmd"
start "Credential Manager Web" cmd /k "%~dp0run-web.cmd"
echo Waiting for API and Web to start...
timeout /t 4 /nobreak >nul
call "%~dp0run-desktop.cmd"
