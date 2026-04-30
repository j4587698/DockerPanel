# DockerPanel

DockerPanel 是一个基于 ASP.NET Core 10 和 Vue 3 的 Docker 管理面板。后端通过 Docker Socket 管理本机 Docker，前端由 Vite 构建后随后端镜像一起发布。

当前后端版本：`0.1.0`

## 技术栈

- 后端：ASP.NET Core 10、SignalR、YARP、TinyDb、Docker.DotNet.Enhanced
- 前端：Vue 3、TypeScript、Vite、Pinia、Vue Router、Element Plus、ECharts、xterm
- 部署：Docker、Docker Compose

## 快速启动

### 生产环境

```bash
git clone https://github.com/j4587698/DockerPanel.git
cd DockerPanel
chmod +x scripts/deploy.sh
./scripts/deploy.sh production
```

默认访问地址：`http://localhost`

如果需要使用其它宿主机端口：

```bash
./scripts/deploy.sh production 9090
```

注意：ACME HTTP-01 证书签发要求公网 80 端口能访问 DockerPanel；如果宿主机不直接使用 80，需要自行配置反向代理或端口转发。

### 开发环境

```bash
./scripts/deploy.sh development
```

- 前端开发服务：`http://localhost:3000`
- 后端开发服务：`http://localhost:5000`
- Swagger 仅在后端 Development 环境启用：`http://localhost:5000/swagger`

也可以分开运行：

```bash
cd Backend/DockerPanel.API
dotnet run
```

```bash
cd Frontend
npm install
npm run dev
```

## 首次使用

- 首次访问会进入 `/setup`，用于创建第一个管理员账号。
- 也可以通过环境变量预置管理员账号：`DOCKERPANEL_ADMIN_USERNAME`、`DOCKERPANEL_ADMIN_PASSWORD`。
- JWT 密钥未配置时会自动生成并保存到数据目录；生产环境建议显式设置 `DOCKERPANEL_JWT_SECRET` 或 `Auth__JwtSecret`。

## 已包含的主要功能

- 登录认证、首次安装、用户与角色权限控制
- 容器、镜像、网络、存储卷管理
- 容器日志、容器终端、文件管理与资源统计
- Compose 项目管理与部署
- 镜像仓库配置与镜像推送/拉取任务进度
- ACME 证书管理与 HTTP-01 挑战校验
- YARP 反向代理映射管理
- 本地/远程节点管理、SSH 连接与终端
- 系统设置、操作审计、后台任务与实时通知

## 项目结构

```text
DockerPanel/
├── Backend/
│   ├── DockerPanel.sln
│   └── DockerPanel.API/
│       ├── Controllers/
│       ├── Models/
│       ├── Services/
│       ├── Hubs/
│       ├── Extensions/
│       ├── Serialization/
│       └── Program.cs
├── Frontend/
│   ├── src/
│   │   ├── api/
│   │   ├── components/
│   │   ├── layout/
│   │   ├── locales/
│   │   ├── router/
│   │   ├── services/
│   │   ├── stores/
│   │   ├── types/
│   │   ├── utils/
│   │   └── views/
│   ├── package.json
│   └── vite.config.ts
├── scripts/
│   ├── build.sh
│   └── deploy.sh
├── docker-compose.yml
├── docker-compose.dev.yml
└── Dockerfile
```

## 配置

常用环境变量：

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境 |
| `ASPNETCORE_URLS` | `http://+:80` | 容器内监听地址 |
| `HTTP_PORT` | `80` | 容器内 HTTP 端口 |
| `ENABLE_HTTPS` | `false` | Docker 部署默认关闭 Kestrel HTTPS |
| `DOCKERPANEL_HTTP_HOST_PORT` | `80` | 生产 Compose 映射到宿主机的 HTTP 端口 |
| `DOCKERPANEL_ADMIN_USERNAME` | - | 首次启动时预置管理员用户名 |
| `DOCKERPANEL_ADMIN_PASSWORD` | - | 首次启动时预置管理员密码 |
| `DOCKERPANEL_JWT_SECRET` | 自动生成 | JWT 签名密钥，生产环境建议显式配置 |
| `TinyDb__Path` | `Data/DockerPanel.db` | TinyDb 数据库路径 |

生产 Compose 会挂载：

- `/var/run/docker.sock:/var/run/docker.sock`：用于管理宿主机 Docker
- `dockerpanel_data:/app/Data`：用于持久化 TinyDb 数据和自动生成的 JWT 密钥

## 构建与验证

后端构建：

```bash
dotnet build Backend/DockerPanel.API/DockerPanel.API.csproj
```

前端类型检查与构建：

```bash
cd Frontend
npm install
./node_modules/.bin/vue-tsc --noEmit
npm run build
```

Docker 镜像构建：

```bash
./scripts/build.sh
```

## GitHub Actions 镜像发布

工作流文件：`.github/workflows/docker-publish.yml`

发布逻辑：

1. 读取 `Backend/DockerPanel.API/DockerPanel.API.csproj` 中的 `<Version>`。
2. 如果远端已存在 `v<Version>` tag，则跳过 Docker 发布。
3. 如果 tag 不存在，则构建根目录 `Dockerfile`，推送到 Docker Hub 和 GitHub Container Registry。
4. 发布成功后创建 `v<Version>` Git tag。

当前 `0.1.0` 版本会发布以下镜像 tag：

- `0.1.0`
- `0.1`
- `latest`（默认分支发布时）

需要在 GitHub 仓库 `Settings` → `Secrets and variables` → `Actions` 中配置：

| Secret | 说明 |
|--------|------|
| `DOCKERHUB_USERNAME` | Docker Hub 用户名或组织名 |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |

GitHub Container Registry 使用 Actions 自动提供的 `GITHUB_TOKEN`，同时仓库 Actions 权限需要允许写入。

## 常用命令

```bash
# 查看生产环境日志
docker-compose -f docker-compose.yml logs -f

# 停止生产环境
docker-compose -f docker-compose.yml down

# 查看生产环境状态
docker-compose -f docker-compose.yml ps

# 健康检查
curl http://localhost/health/live
```
