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
echo Starting Web
echo   本机:     http://localhost:8890
powershell -NoProfile -Command "Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.)' } | ForEach-Object { Write-Host ('  局域网:   http://{0}:8890' -f $_.IPAddress) }"
echo 请另开窗口先运行 scripts\run-api.cmd（http://127.0.0.1:5080），否则登录会失败。
echo 手机/其他电脑请用上面的局域网地址。若打不开，用管理员运行 scripts\allow-lan.cmd 放行防火墙。
echo.
call npm run dev
if errorlevel 1 pause
