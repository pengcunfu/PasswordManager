# 凭据管理器 (Credential Manager)

自托管多端凭据管理器：.NET 10 服务端 + React 响应式 Web + WPF 桌面外壳。Web 与桌面共用同一套前端，桌面通过 WebView2 打开你填写的服务器地址。敏感字段（密码、备注、隐藏自定义字段）在浏览器端使用 AES-GCM 加密，服务端只保存密文。

## 目录结构

```
PasswordManager.Net/
├── src/
│   ├── PasswordManager.Api/       # ASP.NET Core 10 Web API，生产环境托管 SPA
│   ├── PasswordManager.Web/       # Vite + React + TypeScript
│   └── PasswordManager.Desktop/   # WPF + WebView2 外壳
├── deploy/
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── .env.example
├── data/                          # SQLite 数据目录（本地开发 / Docker 卷）
├── PasswordManager.slnx
└── global.json
```

## 本地开发

需要：.NET 10 SDK、Node.js 22+。桌面端还需要 Windows 与 WebView2 Runtime。

终端 1 — 启动 API（http://localhost:5080）：

```bash
dotnet run --project src/PasswordManager.Api
```

终端 2 — 启动前端（http://localhost:5173，`/api` 会代理到 5080）：

```bash
cd src/PasswordManager.Web
npm install
npm run dev
```

浏览器打开 `http://localhost:5173`，注册账号后即可使用。主密码用于登录，并在本地派生加密密钥。

### 桌面端

```bash
dotnet run --project src/PasswordManager.Desktop
```

首次启动填写服务器地址：

- 本地前后端分离开发：`http://localhost:5173`
- Docker / 生产（API 托管 SPA）：`http://主机:8080`

地址保存在 `%USERPROFILE%\Documents\FNSoftware\PasswordManager\desktop.json`。

旧版单机 `passwords.json` 不会自动导入，仍位于文档目录下的原路径。

## Docker 部署

```bash
cd deploy
copy .env.example .env
# 编辑 .env，把 JWT_SIGNING_KEY 改成足够长的随机字符串
docker compose up -d --build
```

访问 `http://localhost:8080`。SQLite 文件在仓库 `data/vault.db`。对外网使用时请在前面加 Caddy / Nginx / Traefik 做 HTTPS。

## 功能

- 多用户注册 / JWT 登录
- 密码条目与分组 CRUD、搜索
- 客户端字段级加密（PBKDF2 + AES-GCM）
- 密码生成器、备份导出、关于
- AI 助手（OpenAI 兼容接口，设置保存在服务端，工具在浏览器执行）
- 响应式布局：PC 三栏+AI，手机列表/详情/底栏

## 安全说明

- 服务端不落盘明文密码字段
- 生产环境必须通过 `Jwt__SigningKey` 注入签名密钥
- 建议全程 HTTPS；HSTS 在反向代理开启
