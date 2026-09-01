# DockerPanel

DockerPanel 是一个基于 **ASP.NET Core 10** 和 **Vue 3** 构建的高性能现代化 Docker 可视化管理与反向代理网关面板。

后端通过 Docker Socket 全面接管并调度 Docker 容器生命周期，前端通过静态资源一体化嵌入后端容器中，支持**单镜像开箱即用、一键部署与无缝平滑自升级**。

当前版本：`0.9.6`

Docker 镜像发布于 Docker Hub：[`j4587698/dockerpanel`](https://hub.docker.com/r/j4587698/dockerpanel)，由 GitHub Actions 自动化构建与发布多架构镜像。

---

## ✨ 核心特性

- **自身一键平滑升级**：内置基于 Sidecar 容器与 Docker Registry Manifest 摘要比对的自升级引擎，数据卷与运行参数 100% 无损继承，一键完成更新重启与健康探活。
- **容器与 Compose 深度管理**：
  - 容器创建、停止、启动、重启、高级重建（Recreate，保留原有卷、网络与端口参数）、批量更新与回滚；
  - 实时日志流推送（SignalR）、Web 终端（xterm.js）、容器内文件浏览与管理、CPU/内存/网络/磁盘实时监控；
  - Docker Compose 项目在线编辑、实时构建与编排部署。
- **镜像与仓库集成**：
  - 支持 Docker Hub、私有 Harbor、阿里云/腾讯云加速器等多种 Registry 认证；
  - 实时镜像拉取/推送进度按层聚合广播；
  - 基于镜像 Digest 的新版本自动检测与通知。
- **YARP 反向代理与网关**：
  - 基于微软高性能 YARP 反向代理核心，支持动态路由规则、负载均衡、SSL/TLS 卸载与路由健康检查。
- **ACME 自动化证书生命周期管理**：
  - 支持 Let's Encrypt / ZeroSSL 自动化证书申请（HTTP-01 与 DNS-01 校验）；
  - 证书自动续期、SNI 动态证书加载与通配符证书关联。
- **多节点调度与 SSH 管理**：
  - 支持管理本地 Docker 引擎与多远程节点；
  - 内置 SSH 密钥管理与基于 Web 的 Linux 服务器终端。
- **用户权限与安全审计**：
  - 多用户与细粒度角色权限管理、初始化向导与安全密码策略；
  - 全流程操作审计日志追踪。

---

## 🛠️ 技术栈

- **后端**：ASP.NET Core 10 (C# 14 / Native AOT JSON 源码生成)、SignalR 实时通信、YARP 反向代理、TinyDb 轻量持久化、AcmeForge ACME 客户端、Docker.DotNet。
- **前端**：Vue 3 (Composition API / `<script setup>`)、TypeScript、Vite、Pinia、Vue Router、Element Plus、ECharts、xterm.js、CodeMirror 6。
- **部署**：Docker / Docker Compose 单容器一体化部署。

---

## 🚀 快速部署

DockerPanel 以**单一镜像**发布，无需额外配置外部数据库。

镜像地址：`j4587698/dockerpanel`

可用 tag：
- `latest`：最新稳定发布版本
- `0.9` / `0.9.6`：对应指定语义化版本

---

### 方式一：使用 docker run 部署（推荐）

#### 1. 标准双端口模式（推荐，支持 HTTP 访问、ACME 证书申请及 HTTPS 反代）

```bash
docker run -d \
  --name dockerpanel \
  --restart unless-stopped \
  -p 80:80 \
  -p 443:443 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e HTTP_PORT=80 \
  -e HTTPS_PORT=443 \
  -e ENABLE_HTTPS=true \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /opt/dockerpanel/data:/app/Data \
  -v /opt/dockerpanel/logs:/app/Logs \
  j4587698/dockerpanel:latest
```

#### 2. 自定义端口模式（如服务器 80/443 已被其他服务占用）

```bash
docker run -d \
  --name dockerpanel \
  --restart unless-stopped \
  -p 8080:80 \
  -p 8443:443 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e HTTP_PORT=80 \
  -e HTTPS_PORT=443 \
  -e ENABLE_HTTPS=true \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /opt/dockerpanel/data:/app/Data \
  -v /opt/dockerpanel/logs:/app/Logs \
  j4587698/dockerpanel:latest
```

> **📌 端口与网络说明**：
> - **80 端口**：面板 HTTP 访问入口，同时用于 **Let's Encrypt / ACME HTTP-01 证书申请校验**。若要在面板中自动申请 SSL 证书，请确保宿主机公网 80 端口能直接访问或通过上游网关转发至本容器。
> - **443 端口**：面板 HTTPS 及 YARP 反代 SNI 动态证书访问入口。

---

### 方式二：使用 Docker Compose 部署

仓库提供 `docker-compose.yml`（或 `docker-compose.hub.yml`），直接引用官方镜像：

```yaml
services:
  dockerpanel:
    image: j4587698/dockerpanel:latest
    container_name: dockerpanel
    ports:
      - "${DOCKERPANEL_HTTP_HOST_PORT:-80}:80"
      - "${DOCKERPANEL_HTTPS_HOST_PORT:-443}:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - HTTP_PORT=80
      - HTTPS_PORT=443
      - ENABLE_HTTPS=true
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft.AspNetCore=Warning
      # 可选：首次启动预置管理员用户名与密码（未设置时首次访问进入 /setup 向导创建）
      # - DOCKERPANEL_ADMIN_USERNAME=admin
      # - DOCKERPANEL_ADMIN_PASSWORD=YourStrongPassword!
      # 可选：显式指定 JWT 密钥（未配置时自动生成并保存到持久化数据目录）
      # - DOCKERPANEL_JWT_SECRET=your-custom-jwt-secret-key-at-least-32-chars
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - dockerpanel_data:/app/Data
      - dockerpanel_logs:/app/Logs
    networks:
      - dockerpanel-network
    extra_hosts:
      - "host.docker.internal:host-gateway"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "-q", "-O", "/dev/null", "http://localhost/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

volumes:
  dockerpanel_data:
    driver: local
  dockerpanel_logs:
    driver: local

networks:
  dockerpanel-network:
    driver: bridge
    name: dockerpanel-network
```

启动与管理：

```bash
# 启动
docker compose up -d

# 查看状态
docker compose ps

# 查看实时日志
docker compose logs -f

# 停止
docker compose down
```

如需自定义宿主机端口，可通过环境变量启动：
```bash
DOCKERPANEL_HTTP_HOST_PORT=8080 DOCKERPANEL_HTTPS_HOST_PORT=8443 docker compose up -d
```

---

### 方式三：一键部署脚本（单文件自举）

无需手动 clone 仓库，直接在终端执行脚本即可全自动部署：

```bash
# 默认部署 (HTTP: 80, HTTPS: 443)
curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- production 80 443

# 自定义端口部署 (如 HTTP 8080, HTTPS 8443)
curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- production 8080 8443
```

---

## 🔄 面板版本升级

DockerPanel 支持多种平滑升级方式：

### 1. Web 界面一键自升级（最便捷）
登录面板后，进入 **【系统设置】 $\to$ 【系统镜像与升级】**：
- 系统会自动基于镜像 Digest 对比远程仓库是否有新版本构建；
- 点击 **「一键升级系统」**，面板将自动预拉取新镜像，并通过短暂的 Sidecar Helper 容器平滑完成停旧建新与健康探活，**所有挂载数据与配置 100% 完整保留**。

### 2. Docker Compose 终端升级
```bash
docker compose pull
docker compose up -d
```

### 3. Docker CLI 终端升级
```bash
docker pull j4587698/dockerpanel:latest
docker stop dockerpanel && docker rm dockerpanel
# 重新执行您的 docker run 命令启动即可（数据在挂载目录中不受影响）
```

---

## ⚙️ 环境变量配置参考

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境（`Production` / `Development`） |
| `HTTP_PORT` | `80` | 容器内 HTTP 监听端口 |
| `HTTPS_PORT` | `443` | 容器内 HTTPS 监听端口 |
| `ENABLE_HTTPS` | `true` | 是否启用 Kestrel HTTPS 监听与 SNI 证书动态加载 |
| `DOCKERPANEL_HTTP_HOST_PORT` | `80` | Compose 部署映射到宿主机的 HTTP 端口 |
| `DOCKERPANEL_HTTPS_HOST_PORT` | `443` | Compose 部署映射到宿主机的 HTTPS 端口 |
| `DOCKERPANEL_ADMIN_USERNAME` | - | 首次启动预置管理员用户名 |
| `DOCKERPANEL_ADMIN_PASSWORD` | - | 首次启动预置管理员密码 |
| `DOCKERPANEL_JWT_SECRET` | 自动持久化 | JWT 签名密钥（建议显式设置 32 位以上强密钥） |
| `TinyDb__Path` | `Data/DockerPanel.db` | TinyDb 数据库持久化路径 |

---

## 💻 本地开发指南

如需参与 DockerPanel 的开发或本地构建：

```bash
# 1. 克隆代码库
git clone https://github.com/j4587698/DockerPanel.git
cd DockerPanel

# 2. 启动后端 (ASP.NET Core 10)
cd Backend/DockerPanel.API
dotnet run

# 3. 启动前端 (Vue 3 + Vite) - 另开终端
cd Frontend
npm install
npm run dev
```

- 前端开发服务：`http://localhost:3000`
- 后端开发接口：`http://localhost:5000`
- Swagger 文档仅在 Development 环境开启：`http://localhost:5000/swagger`

前端类型检查与打包：
```bash
cd Frontend
npm run build
```

---

## 🚢 自动化镜像构建与发布

本项目通过 GitHub Actions 自动化工作流（`.github/workflows/docker-publish.yml`）进行多架构镜像构建与发布：
1. 每次合并到 `main` 分支时，自动提取 `Backend/DockerPanel.API/DockerPanel.API.csproj` 中的 `<Version>`；
2. 自动打包前端并与 .NET 10 后端一同编译为单一轻量化镜像；
3. 推送至 Docker Hub（`j4587698/dockerpanel:latest`、`j4587698/dockerpanel:0.9.6`、`j4587698/dockerpanel:0.9`）及 GHCR。

---

## 📄 开源许可证

本项目基于 [MIT License](LICENSE) 开源。
