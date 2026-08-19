@echo off
chcp 65001 >nul
net session >nul 2>&1
if errorlevel 1 (
  echo 需要管理员权限才能添加防火墙规则。
  echo 请右键此脚本，选择“以管理员身份运行”。
  pause
  exit /b 1
)

netsh advfirewall firewall delete rule name="Credential Manager Web" >nul 2>&1
netsh advfirewall firewall delete rule name="Credential Manager API" >nul 2>&1

netsh advfirewall firewall add rule name="Credential Manager Web" dir=in action=allow protocol=TCP localport=8890
netsh advfirewall firewall add rule name="Credential Manager API" dir=in action=allow protocol=TCP localport=5080

echo.
echo 已放行 TCP 8890（Web）和 5080（API），局域网设备可访问。
pause
