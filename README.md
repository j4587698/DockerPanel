# DockerPanel

DockerPanel 是一个基于 ASP.NET Core 10 和 Vue 3 的 Docker 管理面板。后端通过 Docker Socket 管理本机 Docker，前端由 Vite 构建后随后端镜像一起发布。

当前后端版本：`0.3.0`

Docker 镜像发布在 Docker Hub：[`j4587698/dockerpanel`](https://hub.docker.com/r/j4587698/dockerpanel)，由 GitHub Actions 自动构建并推送，无需本地构建。

## 技术栈

- 后端：ASP.NET Core 10、SignalR、YARP、TinyDb、Docker.DotNet.Enhanced
- 前端：Vue 3、TypeScript、Vite、Pinia、Vue Router、Element Plus、ECharts、xterm
- 部署：Docker、Docker Compose

## 部署

DockerPanel 以**单一镜像**形式发布到 Docker Hub（前端构建产物随后端镜像一起发布），部署只需拉取镜像并运行。

镜像地址：`j4587698/dockerpanel`

可用 tag：

- `latest`：最新发布版本
- `0.3` / `0.3.0`：对应后端 `<Version>`（见 `Backend/DockerPanel.API/DockerPanel.API.csproj`）

### 方式一：使用 docker run

```bash
docker run -d \
  --name dockerpanel-app \
  --restart unless-stopped \
  -p 80:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:80 \
  -e HTTP_PORT=80 \
  -e ENABLE_HTTPS=false \
  -e DOCKERPANEL_JWT_SECRET=你的强密钥 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v dockerpanel_data:/app/Data \
  -v dockerpanel_logs:/app/Logs \
  j4587698/dockerpanel:latest
```

### 方式二：使用 docker compose（推荐）

仓库提供 `docker-compose.hub.yml`，已直接引用 Docker Hub 镜像，无需本地构建：

```bash
# 拉取最新镜像并启动
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d

# 查看状态
docker compose -f docker-compose.hub.yml ps

# 查看日志
docker compose -f docker-compose.hub.yml logs -f

# 停止
docker compose -f docker-compose.hub.yml down
```

如需使用非 80 宿主机端口，通过环境变量覆盖：

```bash
DOCKERPANEL_HTTP_HOST_PORT=9090 docker compose -f docker-compose.hub.yml up -d
```

注意：ACME HTTP-01 证书签发要求公网 80 端口能访问 DockerPanel；如果宿主机不直接使用 80，需要自行配置反向代理或端口转发。

### 方式三：一键部署脚本（推荐，单文件自举）

无需 clone 仓库，直接运行脚本即可。脚本会自动从 GitHub 下载 compose 文件并拉取最新镜像：

```bash
curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- production 80
```

参数：`deploy.sh [production|development] [port]`。生产环境默认端口 80。

脚本逻辑：

- 生产环境：下载 `docker-compose.hub.yml` 到临时目录 → 拉取 `j4587698/dockerpanel:latest` → 启动。
- 开发环境：需在本地仓库内运行（需要源码构建前后端）。

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
├── docker-compose.yml        # 生产部署（使用 Docker Hub 镜像 j4587698/dockerpanel）
├── docker-compose.hub.yml    # 同上，显式引用 Docker Hub 镜像的部署文件
├── docker-compose.dev.yml    # 开发环境（前后端分离）
└── Dockerfile                # CI 构建用，将前端打包进后端镜像
```

## 配置

常用环境变量：

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境 |
| `ASPNETCORE_URLS` | `http://+:80` | 容器内监听地址 |
| `HTTP_PORT` | `80` | 容器内 HTTP 端口 |
| `ENABLE_HTTPS` | `false` | Docker 部署默认关闭 Kestrel HTTPS |
| `DOCKERPANEL_HTTP_HOST_PORT` | `80` | `docker-compose.hub.yml` 映射到宿主机的 HTTP 端口 |
| `DOCKERPANEL_ADMIN_USERNAME` | - | 首次启动时预置管理员用户名 |
| `DOCKERPANEL_ADMIN_PASSWORD` | - | 首次启动时预置管理员密码 |
| `DOCKERPANEL_JWT_SECRET` | 自动生成 | JWT 签名密钥，生产环境建议显式配置 |
| `TinyDb__Path` | `Data/DockerPanel.db` | TinyDb 数据库路径 |

生产部署（`docker-compose.hub.yml`）会挂载：

- `/var/run/docker.sock:/var/run/docker.sock`：用于管理宿主机 Docker
- `dockerpanel_data:/app/Data`：用于持久化 TinyDb 数据和自动生成的 JWT 密钥
- `dockerpanel_logs:/app/Logs`：日志文件

## 本地开发（仅开发环境，生产镜像由 CI 构建）

如需本地修改并运行前后端：

```bash
# 后端
cd Backend/DockerPanel.API
dotnet run

# 前端（另开终端）
cd Frontend
npm install
npm run dev
```

- 前端开发服务：`http://localhost:3000`
- 后端开发服务：`http://localhost:5000`
- Swagger 仅在后端 Development 环境启用：`http://localhost:5000/swagger`

前端类型检查与构建：

```bash
cd Frontend
npm install
./node_modules/.bin/vue-tsc --noEmit
npm run build
```

生产镜像由 GitHub Actions 自动构建并推送到 Docker Hub，见下文。

## 镜像发布（GitHub Actions）

工作流文件：`.github/workflows/docker-publish.yml`

工作流文件：`.github/workflows/docker-publish.yml`

发布流程（全部在 CI 完成，无需本地构建镜像）：

1. 推送代码到默认分支触发工作流。
2. 读取 `Backend/DockerPanel.API/DockerPanel.API.csproj` 中的 `<Version>`。
3. 如果远端已存在 `v<Version>` tag，则跳过 Docker 发布（避免重复发布同一版本）。
4. 否则构建根目录 `Dockerfile`（前端 + 后端一体），推送到 Docker Hub 和 GitHub Container Registry。
5. 发布成功后创建 `v<Version>` Git tag。

> 升级版本时，只需修改 csproj 中的 `<Version>` 并推送，CI 会自动发布新 tag 的镜像。

当前 `0.3.0` 版本会发布以下镜像 tag：

- `0.3.0`
- `0.3`
- `latest`（默认分支发布时）

需要在 GitHub 仓库 `Settings` → `Secrets and variables` → `Actions` 中配置：

| Secret | 说明 |
|--------|------|
| `DOCKERHUB_USERNAME` | Docker Hub 用户名或组织名 |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |

GitHub Container Registry 使用 Actions 自动提供的 `GITHUB_TOKEN`，同时仓库 Actions 权限需要允许写入。

## 常用命令

```bash
# 拉取最新镜像并启动
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d

# 查看运行状态
docker compose -f docker-compose.hub.yml ps

# 查看日志
docker compose -f docker-compose.hub.yml logs -f

# 停止
docker compose -f docker-compose.hub.yml down

# 健康检查
curl http://localhost/health/live
```
